using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NekoLib.Data.Gateway;
using NekoLib.Data.Query;
using Xunit;

namespace NekoLib.Data.Tests.Unit.Gateway
{
    public class TypeAdaptationTests
    {
        [Fact]
        public void DatabaseGatewayOptions_Defaults_RequireExplicitPromotionAndRejectLoss()
        {
            DatabaseGatewayOptions options = new DatabaseGatewayOptions();

            Assert.Equal(TypePromotionPolicy.ExplicitOnly, options.TypePromotionPolicy);
            Assert.Equal(TypeDecayPolicy.AllowFallback, options.TypeDecayPolicy);
            Assert.Equal(TypeLossPolicy.RejectPotentialLoss, options.TypeLossPolicy);
            Assert.Equal(SchemaDiscoveryMode.Lazy, options.SchemaDiscoveryMode);
            Assert.NotEmpty(options.AutomaticPromotionRules);
            Assert.All(
                options.AutomaticDecayRules,
                rule => Assert.Equal(TypeAdaptationLoss.Lossless, rule.Loss));
        }

        [Fact]
        public async Task Insert_ExplicitPromotion_BindsPromotedValueAndReportsOnce()
        {
            FakeNonQueryConnectionFactory factory = CreateFactory();
            using (QueryExecutionContext context = CreateContext(factory))
            {
                DatabaseGateway gateway = new DatabaseGateway(context);
                List<TypeAdaptationEventArgs> events = new List<TypeAdaptationEventArgs>();
                gateway.OnTypeAdaptation += events.Add;

                int result = await gateway.Insert(new QueryBuilder()
                    .InsertInto("Inventory")
                    .Value("Quantity", "54", parameter =>
                        parameter.AllowPromotion(TypePromotions.StringToInt32)));

                Assert.Equal(1, result);
                Assert.Equal(54, ParameterValue(factory.LastConnection.LastCommand, "@p1"));
                TypeAdaptationEventArgs adaptation = Assert.Single(events);
                Assert.Equal(TypeAdaptationKind.Promotion, adaptation.Kind);
                Assert.Equal(typeof(string), adaptation.SourceType);
                Assert.Equal(typeof(int), adaptation.TargetType);
                Assert.Equal("Inventory", adaptation.Table);
                Assert.Equal("Quantity", adaptation.Column);
                Assert.Equal("@p1", adaptation.ParameterName);
                Assert.Equal(TypeAdaptationReasonCode.ExplicitRule, adaptation.ReasonCode);
                Assert.DoesNotContain("54", adaptation.StrategyId);
            }
        }

        [Fact]
        public async Task Insert_PotentiallyLossyPromotion_RequiresPerValueRuleAndGatewayOptIn()
        {
            TypePromotionRule rule = new TypePromotionRule(
                "int32-to-int16-checked",
                typeof(int),
                typeof(short),
                value => checked((short)(int)value),
                TypeAdaptationLoss.PotentiallyLossy);
            QueryBuilder builder = new QueryBuilder()
                .InsertInto("Inventory")
                .Value("Quantity", 54, parameter => parameter.AllowPromotion(rule));

            FakeNonQueryConnectionFactory rejectedFactory = CreateFactory();
            using (QueryExecutionContext context = CreateContext(rejectedFactory))
            {
                DatabaseGateway gateway = new DatabaseGateway(context);
                TypeAdaptationException exception = await Assert.ThrowsAsync<TypeAdaptationException>(() =>
                    gateway.Insert(builder));

                Assert.Equal(
                    TypeAdaptationReasonCode.LossyAdaptationNotAuthorized,
                    exception.ReasonCode);
                Assert.Equal(0, rejectedFactory.LastConnection.LastCommand.ExecuteNonQueryAsyncCalls);
            }

            FakeNonQueryConnectionFactory allowedFactory = CreateFactory();
            DatabaseGatewayOptions options = new DatabaseGatewayOptions
            {
                TypeLossPolicy = TypeLossPolicy.AllowExplicitAndReport
            };
            using (QueryExecutionContext context = CreateContext(allowedFactory, options))
            {
                DatabaseGateway gateway = new DatabaseGateway(context);
                List<TypeAdaptationEventArgs> events = new List<TypeAdaptationEventArgs>();
                gateway.OnTypeAdaptation += events.Add;

                await gateway.Insert(builder);

                Assert.Equal((short)54, ParameterValue(allowedFactory.LastConnection.LastCommand, "@p1"));
                TypeAdaptationEventArgs adaptation = Assert.Single(events);
                Assert.Equal(TypeAdaptationKind.Promotion, adaptation.Kind);
                Assert.Equal(TypeAdaptationLoss.PotentiallyLossy, adaptation.Loss);
            }
        }

        [Fact]
        public async Task Insert_DisabledPromotion_FailsBeforeDispatchWithSanitizedEvidence()
        {
            FakeNonQueryConnectionFactory factory = CreateFactory();
            DatabaseGatewayOptions options = new DatabaseGatewayOptions
            {
                TypePromotionPolicy = TypePromotionPolicy.Disabled
            };
            using (QueryExecutionContext context = CreateContext(factory, options))
            {
                DatabaseGateway gateway = new DatabaseGateway(context);
                int dispatchCalls = 0;
                context.OnSqlDispatch += _ => dispatchCalls++;

                TypeAdaptationException exception = await Assert.ThrowsAsync<TypeAdaptationException>(() =>
                    gateway.Insert(new QueryBuilder()
                        .InsertInto("Inventory")
                        .Value("Quantity", "54", parameter =>
                            parameter.AllowPromotion(TypePromotions.StringToInt32))));

                Assert.Equal(TypeAdaptationReasonCode.PromotionDisabled, exception.ReasonCode);
                Assert.Equal(0, dispatchCalls);
                Assert.Equal(0, factory.LastConnection.LastCommand.ExecuteNonQueryAsyncCalls);
                Assert.Null(exception.InnerException);
                Assert.DoesNotContain("54", exception.Message);
                Assert.DoesNotContain("INSERT", exception.Message, StringComparison.OrdinalIgnoreCase);
            }
        }

        [Theory]
        [InlineData("fifty-four", TypeAdaptationReasonCode.ConversionRejected)]
        [InlineData("999999999999", TypeAdaptationReasonCode.Overflow)]
        public async Task Insert_InvalidExplicitPromotion_FailsLocallyWithoutSensitiveValue(
            string input,
            TypeAdaptationReasonCode expectedReason)
        {
            FakeNonQueryConnectionFactory factory = CreateFactory();
            using (QueryExecutionContext context = CreateContext(factory))
            {
                DatabaseGateway gateway = new DatabaseGateway(context);
                int dispatchCalls = 0;
                context.OnSqlDispatch += _ => dispatchCalls++;

                TypeAdaptationException exception = await Assert.ThrowsAsync<TypeAdaptationException>(() =>
                    gateway.Insert(new QueryBuilder()
                        .InsertInto("Inventory")
                        .Value("Quantity", input, parameter =>
                            parameter.AllowPromotion(TypePromotions.StringToInt32))));

                Assert.Equal(expectedReason, exception.ReasonCode);
                Assert.Equal(0, dispatchCalls);
                Assert.Equal(0, factory.LastConnection.LastCommand.ExecuteNonQueryAsyncCalls);
                Assert.Null(exception.InnerException);
                Assert.DoesNotContain(input, exception.Message);
            }
        }

        [Fact]
        public async Task Insert_AdaptationHookThrows_DatabaseOutcomeRemainsAuthoritative()
        {
            FakeNonQueryConnectionFactory factory = CreateFactory();
            using (QueryExecutionContext context = CreateContext(factory))
            {
                DatabaseGateway gateway = new DatabaseGateway(context);
                int laterHookCalls = 0;
                gateway.OnTypeAdaptation += _ => throw new InvalidOperationException("observer failure");
                gateway.OnTypeAdaptation += _ => laterHookCalls++;

                int result = await gateway.Insert(new QueryBuilder()
                    .InsertInto("Inventory")
                    .Value("Quantity", "54", parameter =>
                        parameter.AllowPromotion(TypePromotions.StringToInt32)));

                Assert.Equal(1, result);
                Assert.Equal(1, laterHookCalls);
                Assert.Equal(1, factory.LastConnection.LastCommand.ExecuteNonQueryAsyncCalls);
            }
        }

        [Fact]
        public async Task Insert_ProviderFailureAfterPromotion_DoesNotRetry()
        {
            InvalidOperationException providerFailure = new InvalidOperationException("provider failed");
            FakeNonQueryConnectionFactory factory = new FakeNonQueryConnectionFactory(
                () => new FakeNonQueryCommand
                {
                    ExecuteException = providerFailure
                });
            using (QueryExecutionContext context = CreateContext(factory))
            {
                DatabaseGateway gateway = new DatabaseGateway(context);
                int adaptations = 0;
                gateway.OnTypeAdaptation += _ => adaptations++;

                InvalidOperationException actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    gateway.Insert(new QueryBuilder()
                        .InsertInto("Inventory")
                        .Value("Quantity", "54", parameter =>
                            parameter.AllowPromotion(TypePromotions.StringToInt32))));

                Assert.Same(providerFailure, actual);
                Assert.Equal(1, adaptations);
                Assert.Equal(1, factory.LastConnection.LastCommand.ExecuteNonQueryAsyncCalls);
            }
        }

        [Fact]
        public async Task Insert_LossyDecayWithoutLossOptIn_FailsBeforeDispatch()
        {
            FakeNonQueryConnectionFactory factory = CreateFactory();
            using (QueryExecutionContext context = CreateAccessContext(factory))
            {
                DatabaseGateway gateway = new DatabaseGateway(context);
                int dispatchCalls = 0;
                context.OnSqlDispatch += _ => dispatchCalls++;

                TypeAdaptationException exception = await Assert.ThrowsAsync<TypeAdaptationException>(() =>
                    gateway.Insert(DateTimeOffsetBuilder(parameter =>
                        parameter.AllowDecay(TypeDecays.DateTimeOffsetToUtcDateTime))));

                Assert.Equal(TypeAdaptationReasonCode.LossyAdaptationNotAuthorized, exception.ReasonCode);
                Assert.Equal(0, dispatchCalls);
                Assert.Equal(0, factory.LastConnection.LastCommand.ExecuteNonQueryAsyncCalls);
            }
        }

        [Fact]
        public async Task Insert_ExplicitLossyDecay_BindsFallbackAndReportsLoss()
        {
            FakeNonQueryConnectionFactory factory = CreateFactory();
            DatabaseGatewayOptions options = new DatabaseGatewayOptions
            {
                TypeLossPolicy = TypeLossPolicy.AllowExplicitAndReport
            };
            using (QueryExecutionContext context = CreateAccessContext(factory, options))
            {
                DatabaseGateway gateway = new DatabaseGateway(context);
                List<TypeAdaptationEventArgs> events = new List<TypeAdaptationEventArgs>();
                gateway.OnTypeAdaptation += events.Add;

                await gateway.Insert(DateTimeOffsetBuilder(parameter =>
                    parameter.AllowDecay(TypeDecays.DateTimeOffsetToUtcDateTime)));

                Assert.IsType<DateTime>(ParameterValue(factory.LastConnection.LastCommand, "@p1"));
                TypeAdaptationEventArgs adaptation = Assert.Single(events);
                Assert.Equal(TypeAdaptationKind.Decay, adaptation.Kind);
                Assert.Equal(TypeAdaptationLoss.PotentiallyLossy, adaptation.Loss);
                Assert.Equal(TypeAdaptationReasonCode.ProviderFallback, adaptation.ReasonCode);
            }
        }

        [Fact]
        public async Task Insert_StrictDecay_RejectsEvenExplicitFallback()
        {
            FakeNonQueryConnectionFactory factory = CreateFactory();
            DatabaseGatewayOptions options = new DatabaseGatewayOptions
            {
                TypeDecayPolicy = TypeDecayPolicy.Strict,
                TypeLossPolicy = TypeLossPolicy.AllowExplicitAndReport
            };
            using (QueryExecutionContext context = CreateAccessContext(factory, options))
            {
                DatabaseGateway gateway = new DatabaseGateway(context);

                TypeAdaptationException exception = await Assert.ThrowsAsync<TypeAdaptationException>(() =>
                    gateway.Insert(DateTimeOffsetBuilder(parameter =>
                        parameter.AllowDecay(TypeDecays.DateTimeOffsetToUtcDateTime))));

                Assert.Equal(TypeAdaptationReasonCode.StrictDecayRejected, exception.ReasonCode);
                Assert.Equal(0, factory.LastConnection.LastCommand.ExecuteNonQueryAsyncCalls);
            }
        }

        [Fact]
        public async Task Insert_CustomTemporalFormatter_RequiresLossAuthorizationAndReportsFormat()
        {
            const string format = "yyyy/MM/dd HH:mm:ss:fff";
            TypeDecayRule formatter = TypeDecays.CreateDateTimeOffsetToString(
                format,
                CultureInfo.InvariantCulture);
            FakeNonQueryConnectionFactory rejectedFactory = CreateFactory();
            using (QueryExecutionContext context = CreateAccessContext(rejectedFactory))
            {
                DatabaseGateway gateway = new DatabaseGateway(context);

                TypeAdaptationException exception = await Assert.ThrowsAsync<TypeAdaptationException>(() =>
                    gateway.Insert(DateTimeOffsetBuilder(parameter =>
                        parameter.AllowDecay(formatter))));

                Assert.Equal(TypeAdaptationReasonCode.LossyAdaptationNotAuthorized, exception.ReasonCode);
                TypeAdaptationAttempt rejected = Assert.Single(exception.Attempts);
                Assert.Equal(format, rejected.Format);
                Assert.Equal(CultureInfo.InvariantCulture.Name, rejected.CultureName);
                Assert.Equal(0, rejectedFactory.LastConnection.LastCommand.ExecuteNonQueryAsyncCalls);
            }

            FakeNonQueryConnectionFactory allowedFactory = CreateFactory();
            DatabaseGatewayOptions options = new DatabaseGatewayOptions
            {
                TypeLossPolicy = TypeLossPolicy.AllowExplicitAndReport
            };
            using (QueryExecutionContext context = CreateAccessContext(allowedFactory, options))
            {
                DatabaseGateway gateway = new DatabaseGateway(context);
                List<TypeAdaptationEventArgs> events = new List<TypeAdaptationEventArgs>();
                gateway.OnTypeAdaptation += events.Add;

                await gateway.Insert(DateTimeOffsetBuilder(parameter =>
                    parameter.AllowDecay(formatter)));

                Assert.Equal(
                    "2026/08/27 10:30:00:000",
                    ParameterValue(allowedFactory.LastConnection.LastCommand, "@p1"));
                TypeAdaptationEventArgs adaptation = Assert.Single(events);
                Assert.Equal(format, adaptation.Format);
                Assert.Equal(CultureInfo.InvariantCulture.Name, adaptation.CultureName);
                Assert.Equal(TypeAdaptationLoss.PotentiallyLossy, adaptation.Loss);
                Assert.Single(adaptation.Attempts);
            }
        }

        [Fact]
        public async Task Insert_OrderedDecayCandidates_ReportsRejectedAlternativeAndSelectedFormatterOnce()
        {
            const string format = "yyyy/MM/dd HH:mm:ss:fff";
            TypeDecayRule preferredRepresentation = new TypeDecayRule(
                "datetimeoffset-native",
                typeof(DateTimeOffset),
                typeof(DateTimeOffset),
                value => value,
                TypeAdaptationLoss.Lossless);
            TypeDecayRule stringFormatter = TypeDecays.CreateDateTimeOffsetToString(
                format,
                CultureInfo.InvariantCulture);
            FakeNonQueryConnectionFactory factory = CreateFactory();
            DatabaseGatewayOptions options = new DatabaseGatewayOptions
            {
                TypeLossPolicy = TypeLossPolicy.AllowExplicitAndReport
            };
            using (QueryExecutionContext context = CreateAccessContext(factory, options))
            {
                DatabaseGateway gateway = new DatabaseGateway(context);
                List<TypeAdaptationEventArgs> events = new List<TypeAdaptationEventArgs>();
                gateway.OnTypeAdaptation += events.Add;

                QueryBuilder builder = DateTimeOffsetBuilder(parameter => parameter
                    .AllowDecay(preferredRepresentation)
                    .AllowDecayFallback(stringFormatter));
                LogicalParameter logical = Assert.Single(builder.Build().LogicalParameters);
                Assert.Equal(2, logical.DecayRules.Count);
                Assert.Same(preferredRepresentation, logical.DecayRule);

                await gateway.Insert(builder);

                TypeAdaptationEventArgs adaptation = Assert.Single(events);
                Assert.Equal(stringFormatter.StrategyId, adaptation.StrategyId);
                Assert.Equal(format, adaptation.Format);
                Assert.Equal(2, adaptation.Attempts.Count);
                Assert.Equal(
                    TypeAdaptationReasonCode.ProviderRepresentationUnsupported,
                    adaptation.Attempts[0].ReasonCode);
                Assert.Equal(
                    TypeAdaptationReasonCode.ProviderFallback,
                    adaptation.Attempts[1].ReasonCode);
                Assert.Equal(1, factory.LastConnection.LastCommand.ExecuteNonQueryAsyncCalls);
            }
        }

        [Fact]
        public async Task Insert_SchemaValidatedPromotion_DiscoversOnceAndCaches()
        {
            FakeNonQueryConnectionFactory factory = CreateSchemaFactory(typeof(int));
            DatabaseGatewayOptions options = new DatabaseGatewayOptions
            {
                TypePromotionPolicy = TypePromotionPolicy.SchemaValidated
            };
            using (QueryExecutionContext context = CreateContext(factory, options))
            {
                DatabaseGateway gateway = new DatabaseGateway(context);

                await gateway.Insert(QuantityBuilder("54"));
                Assert.Equal(54, ParameterValue(factory.LastConnection.LastCommand, "@p1"));
                int callsAfterFirstUse = factory.SchemaCalls;

                await gateway.Insert(QuantityBuilder("62"));
                Assert.Equal(62, ParameterValue(factory.LastConnection.LastCommand, "@p1"));

                Assert.Equal(2, callsAfterFirstUse);
                Assert.Equal(callsAfterFirstUse, factory.SchemaCalls);
            }
        }

        [Fact]
        public async Task Insert_AccessOleDbTypeCode_ResolvesSchemaTargetBeforePromotion()
        {
            FakeNonQueryConnectionFactory factory = new FakeNonQueryConnectionFactory(
                () => new FakeNonQueryCommand { Result = 1 },
                schemaFactory: (collection, restrictions) => CreateOleDbCodeSchema(collection, 3));
            DatabaseGatewayOptions options = new DatabaseGatewayOptions
            {
                TypePromotionPolicy = TypePromotionPolicy.SchemaValidated
            };
            using (QueryExecutionContext context = CreateAccessContext(factory, options))
            {
                DatabaseGateway gateway = new DatabaseGateway(context);

                await gateway.Insert(QuantityBuilder("54"));

                Assert.Equal(54, ParameterValue(factory.LastConnection.LastCommand, "@p1"));
            }
        }

        [Fact]
        public async Task Insert_UnresolvedSchemaType_FailsLocallyInsteadOfPassingOriginalValue()
        {
            FakeNonQueryConnectionFactory factory = new FakeNonQueryConnectionFactory(
                () => new FakeNonQueryCommand { Result = 1 },
                schemaFactory: (collection, restrictions) => CreateUnknownSchema(collection));
            DatabaseGatewayOptions options = new DatabaseGatewayOptions
            {
                TypePromotionPolicy = TypePromotionPolicy.SchemaValidated
            };
            using (QueryExecutionContext context = CreateContext(factory, options))
            {
                DatabaseGateway gateway = new DatabaseGateway(context);

                TypeAdaptationException exception = await Assert.ThrowsAsync<TypeAdaptationException>(() =>
                    gateway.Insert(QuantityBuilder("54")));

                Assert.Equal(TypeAdaptationReasonCode.SchemaUnavailable, exception.ReasonCode);
                Assert.Equal(0, factory.LastConnection.LastCommand.ExecuteNonQueryAsyncCalls);
            }
        }

        [Fact]
        public async Task Insert_ExplicitOnlyWithoutRule_RejectsSemanticPromotionWithoutSchemaLookup()
        {
            FakeNonQueryConnectionFactory factory = CreateSchemaFactory(typeof(int));
            using (QueryExecutionContext context = CreateContext(factory))
            {
                DatabaseGateway gateway = new DatabaseGateway(context);

                TypeAdaptationException exception = await Assert.ThrowsAsync<TypeAdaptationException>(() =>
                    gateway.Insert(new QueryBuilder()
                        .InsertInto("Inventory")
                        .Value("Quantity", "54", parameter => parameter.As<int>())));

                Assert.Equal(TypeAdaptationReasonCode.PromotionRuleMissing, exception.ReasonCode);
                Assert.Equal(0, factory.SchemaCalls);
                Assert.Equal(0, factory.LastConnection.LastCommand.ExecuteNonQueryAsyncCalls);
            }
        }

        [Fact]
        public async Task Insert_DisabledSchemaDiscovery_RejectsSchemaAuthorizedPromotionLocally()
        {
            FakeNonQueryConnectionFactory factory = CreateSchemaFactory(typeof(int));
            DatabaseGatewayOptions options = new DatabaseGatewayOptions
            {
                TypePromotionPolicy = TypePromotionPolicy.SchemaValidated,
                SchemaDiscoveryMode = SchemaDiscoveryMode.Disabled
            };
            using (QueryExecutionContext context = CreateContext(factory, options))
            {
                DatabaseGateway gateway = new DatabaseGateway(context);

                TypeAdaptationException exception = await Assert.ThrowsAsync<TypeAdaptationException>(() =>
                    gateway.Insert(QuantityBuilder("54")));

                Assert.Equal(TypeAdaptationReasonCode.SchemaUnavailable, exception.ReasonCode);
                Assert.Equal(0, factory.SchemaCalls);
                Assert.Equal(0, factory.LastConnection.LastCommand.ExecuteNonQueryAsyncCalls);
            }
        }

        [Fact]
        public async Task Insert_ConcurrentLazyDiscovery_LoadsProviderAndColumnSchemaOnce()
        {
            FakeNonQueryConnectionFactory factory = CreateSchemaFactory(typeof(int));
            DatabaseGatewayOptions options = new DatabaseGatewayOptions
            {
                TypePromotionPolicy = TypePromotionPolicy.SchemaValidated
            };
            using (QueryExecutionContext context = CreateContext(factory, options))
            {
                DatabaseGateway gateway = new DatabaseGateway(context);

                await Task.WhenAll(
                    gateway.Insert(QuantityBuilder("54")),
                    gateway.Insert(QuantityBuilder("62")),
                    gateway.Insert(QuantityBuilder("73")));

                Assert.Equal(2, factory.SchemaCalls);
            }
        }

        [Fact]
        public async Task RefreshSchemaAsync_ReplacesCachedColumnType()
        {
            Type currentColumnType = typeof(int);
            FakeNonQueryConnectionFactory factory = new FakeNonQueryConnectionFactory(
                () => new FakeNonQueryCommand { Result = 1 },
                schemaFactory: (collection, restrictions) =>
                    CreateSchema(collection, currentColumnType));
            DatabaseGatewayOptions options = new DatabaseGatewayOptions
            {
                TypePromotionPolicy = TypePromotionPolicy.SchemaValidated
            };
            using (QueryExecutionContext context = CreateContext(factory, options))
            {
                DatabaseGateway gateway = new DatabaseGateway(context);

                await gateway.Insert(QuantityBuilder("54"));
                Assert.IsType<int>(ParameterValue(factory.LastConnection.LastCommand, "@p1"));

                currentColumnType = typeof(long);
                await gateway.Insert(QuantityBuilder("62"));
                Assert.IsType<int>(ParameterValue(factory.LastConnection.LastCommand, "@p1"));

                await gateway.RefreshSchemaAsync("Inventory", new[] { "Quantity" });
                await gateway.Insert(QuantityBuilder("73"));

                Assert.IsType<long>(ParameterValue(factory.LastConnection.LastCommand, "@p1"));
                Assert.Equal(3, factory.SchemaCalls);
            }
        }

        [Fact]
        public async Task ClearSchemaCache_NextSchemaAuthorizedUseDiscoversAgain()
        {
            FakeNonQueryConnectionFactory factory = CreateSchemaFactory(typeof(int));
            DatabaseGatewayOptions options = new DatabaseGatewayOptions
            {
                TypePromotionPolicy = TypePromotionPolicy.SchemaValidated
            };
            using (QueryExecutionContext context = CreateContext(factory, options))
            {
                DatabaseGateway gateway = new DatabaseGateway(context);

                await gateway.Insert(QuantityBuilder("54"));
                Assert.Equal(2, factory.SchemaCalls);

                gateway.ClearSchemaCache();
                await gateway.Insert(QuantityBuilder("62"));

                Assert.Equal(4, factory.SchemaCalls);
            }
        }

        [Fact]
        public async Task PreloadSchemaAsync_UnavailableColumn_FailsWithoutPretendingToCache()
        {
            FakeNonQueryConnectionFactory factory = new FakeNonQueryConnectionFactory(
                () => new FakeNonQueryCommand { Result = 1 },
                schemaFactory: (collection, restrictions) => CreateSchema(collection, typeof(int)));
            using (QueryExecutionContext context = CreateContext(factory))
            {
                DatabaseGateway gateway = new DatabaseGateway(context);

                TypeAdaptationException exception = await Assert.ThrowsAsync<TypeAdaptationException>(() =>
                    gateway.PreloadSchemaAsync("Inventory", new[] { "MissingColumn" }));

                Assert.Equal(TypeAdaptationReasonCode.SchemaUnavailable, exception.ReasonCode);
                Assert.Null(exception.InnerException);
            }
        }

        [Fact]
        public async Task Insert_SqliteGetSchemaUnavailable_UsesPragmaMetadata()
        {
            int commandIndex = 0;
            FakeNonQueryCommand dmlCommand = null;
            FakeNonQueryConnectionFactory factory = new FakeNonQueryConnectionFactory(
                () =>
                {
                    if (Interlocked.Increment(ref commandIndex) == 2)
                    {
                        return new FakeNonQueryCommand
                        {
                            Reader = new FakeDataReader(
                                new[] { "cid", "name", "type", "notnull", "dflt_value", "pk" },
                                new[]
                                {
                                    typeof(long), typeof(string), typeof(string),
                                    typeof(long), typeof(object), typeof(long)
                                },
                                new object[] { 0L, "Quantity", "INTEGER", 1L, DBNull.Value, 0L })
                        };
                    }

                    dmlCommand = new FakeNonQueryCommand { Result = 1 };
                    return dmlCommand;
                },
                schemaFactory: (collection, restrictions) =>
                    throw new NotSupportedException("GetSchema is unavailable."));
            DatabaseGatewayOptions options = new DatabaseGatewayOptions
            {
                TypePromotionPolicy = TypePromotionPolicy.SchemaValidated
            };
            using (QueryExecutionContext context = CreateContext(factory, options))
            {
                DatabaseGateway gateway = new DatabaseGateway(context);

                await gateway.Insert(QuantityBuilder("54"));

                Assert.Equal(54L, ParameterValue(dmlCommand, "@p1"));
                Assert.Equal(2, commandIndex);
            }
        }

        [Fact]
        public async Task Insert_UnknownProviderCannotAuthorizeAutomaticPromotion()
        {
            FakeNonQueryConnectionFactory factory = CreateSchemaFactory(typeof(int));
            DatabaseGatewayOptions options = new DatabaseGatewayOptions
            {
                TypePromotionPolicy = TypePromotionPolicy.SchemaValidated
            };
            using (QueryExecutionContext context = new QueryExecutionContext(
                factory,
                new UnknownTranslator(),
                options))
            {
                DatabaseGateway gateway = new DatabaseGateway(context);

                TypeAdaptationException exception = await Assert.ThrowsAsync<TypeAdaptationException>(() =>
                    gateway.Insert(QuantityBuilder("54")));

                Assert.Equal(TypeAdaptationReasonCode.UnknownProvider, exception.ReasonCode);
                Assert.Equal(0, factory.SchemaCalls);
                Assert.Equal(0, factory.LastConnection.LastCommand.ExecuteNonQueryAsyncCalls);
            }
        }

        [Fact]
        public async Task Insert_PreloadModeWithoutPreload_FailsThenUsesExplicitPreload()
        {
            FakeNonQueryConnectionFactory factory = CreateSchemaFactory(typeof(int));
            DatabaseGatewayOptions options = new DatabaseGatewayOptions
            {
                TypePromotionPolicy = TypePromotionPolicy.SchemaValidated,
                SchemaDiscoveryMode = SchemaDiscoveryMode.Preload
            };
            using (QueryExecutionContext context = CreateContext(factory, options))
            {
                DatabaseGateway gateway = new DatabaseGateway(context);
                TypeAdaptationException exception = await Assert.ThrowsAsync<TypeAdaptationException>(() =>
                    gateway.Insert(QuantityBuilder("54")));
                Assert.Equal(TypeAdaptationReasonCode.SchemaNotPreloaded, exception.ReasonCode);

                await gateway.PreloadSchemaAsync("Inventory", new[] { "Quantity" });
                await gateway.Insert(QuantityBuilder("54"));

                Assert.Equal(54, ParameterValue(factory.LastConnection.LastCommand, "@p1"));
            }
        }

        [Fact]
        public async Task Insert_LazySchemaInsideTransaction_FailsBeforeDispatch()
        {
            FakeNonQueryConnectionFactory factory = CreateSchemaFactory(typeof(int));
            DatabaseGatewayOptions options = new DatabaseGatewayOptions
            {
                TypePromotionPolicy = TypePromotionPolicy.SchemaValidated
            };
            using (QueryExecutionContext context = CreateContext(factory, options))
            {
                DatabaseGateway gateway = new DatabaseGateway(context);
                using (DbSession session = await gateway.OpenSessionAsync())
                {
                    session.BeginTransaction();
                    int dispatchCalls = 0;
                    context.OnSqlDispatch += _ => dispatchCalls++;

                    TypeAdaptationException exception = await Assert.ThrowsAsync<TypeAdaptationException>(() =>
                        gateway.Insert(QuantityBuilder("54"), session));

                    Assert.Equal(TypeAdaptationReasonCode.SchemaRequiredBeforeTransaction, exception.ReasonCode);
                    Assert.Equal(0, dispatchCalls);
                    Assert.Equal(0, factory.LastConnection.LastCommand.ExecuteNonQueryAsyncCalls);
                }
            }
        }

        [Fact]
        public async Task Insert_AutomaticLosslessDecay_UsesSchemaTargetAndReports()
        {
            FakeNonQueryConnectionFactory factory = CreateSchemaFactory(typeof(string));
            using (QueryExecutionContext context = CreateAccessContext(factory))
            {
                DatabaseGateway gateway = new DatabaseGateway(context);
                List<TypeAdaptationEventArgs> events = new List<TypeAdaptationEventArgs>();
                gateway.OnTypeAdaptation += events.Add;

                await gateway.Insert(DateTimeOffsetBuilder(null));

                string bound = Assert.IsType<string>(
                    ParameterValue(factory.LastConnection.LastCommand, "@p1"));
                Assert.Contains("T", bound);
                TypeAdaptationEventArgs adaptation = Assert.Single(events);
                Assert.Equal(TypeAdaptationLoss.Lossless, adaptation.Loss);
                Assert.Equal(typeof(string), adaptation.TargetType);
            }
        }

        [Fact]
        public async Task Insert_RepeatedPositionalParameter_PromotesAndReportsOnce()
        {
            FakeNonQueryConnectionFactory factory = CreateFactory();
            DatabaseGatewayOptions options = new DatabaseGatewayOptions
            {
                ParameterBindingMode = DbParameterBindingMode.Positional
            };
            using (QueryExecutionContext context = new QueryExecutionContext(
                factory,
                new RepeatingParameterTranslator(),
                options))
            {
                DatabaseGateway gateway = new DatabaseGateway(context);
                int adaptationCalls = 0;
                gateway.OnTypeAdaptation += _ => adaptationCalls++;

                QueryBuilder builder = new QueryBuilder()
                    .Update("Inventory")
                    .Set("Quantity", "54", parameter =>
                        parameter.AllowPromotion(TypePromotions.StringToInt32))
                    .Where("Id", QueryOperator.Equal, 7);

                await gateway.Update(builder);

                Assert.Equal(1, adaptationCalls);
                Assert.Equal(
                    new object[] { 54, 54, 7 },
                    ParameterValues(factory.LastConnection.LastCommand));
            }
        }

        [Fact]
        public async Task Update_SubqueryLogicalParameter_IsRenamedPromotedAndReportedOnce()
        {
            FakeNonQueryConnectionFactory factory = CreateFactory();
            using (QueryExecutionContext context = CreateContext(factory))
            {
                DatabaseGateway gateway = new DatabaseGateway(context);
                int adaptationCalls = 0;
                gateway.OnTypeAdaptation += _ => adaptationCalls++;

                QueryBuilder subquery = new QueryBuilder()
                    .Select("Id")
                    .From("InventoryHistory")
                    .Where("PreviousQuantity", QueryOperator.Equal, "54", parameter =>
                        parameter.AllowPromotion(TypePromotions.StringToInt32));
                QueryBuilder update = new QueryBuilder()
                    .Update("Inventory")
                    .Set("Quantity", 55)
                    .WhereExists(subquery);

                await gateway.Update(update);

                Assert.Equal(1, adaptationCalls);
                Assert.Contains(
                    factory.LastConnection.LastCommand.Parameters.Cast<DbParameter>(),
                    parameter => Equals(parameter.Value, 54));
                Assert.Equal(1, factory.LastConnection.LastCommand.ExecuteNonQueryAsyncCalls);
            }
        }

        [Fact]
        public async Task Insert_RawSqlParameters_AreNotSchemaPromoted()
        {
            FakeNonQueryConnectionFactory factory = CreateSchemaFactory(typeof(int));
            DatabaseGatewayOptions options = new DatabaseGatewayOptions
            {
                TypePromotionPolicy = TypePromotionPolicy.SchemaValidated
            };
            using (QueryExecutionContext context = CreateContext(factory, options))
            {
                DatabaseGateway gateway = new DatabaseGateway(context);

                await gateway.Insert(
                    "INSERT INTO Inventory (Quantity) VALUES (@p1)",
                    new Dictionary<string, object> { { "@p1", "54" } });

                Assert.Equal("54", ParameterValue(factory.LastConnection.LastCommand, "@p1"));
                Assert.Equal(0, factory.SchemaCalls);
            }
        }

        [Fact]
        public void Build_ConfiguredParameter_PreservesLogicalProvenanceThroughTranslator()
        {
            QueryModel model = new QueryBuilder()
                .InsertInto("Inventory")
                .Value("Quantity", "54", parameter =>
                    parameter.AllowPromotion(TypePromotions.StringToInt32))
                .Build();

            LogicalParameter logical = Assert.Single(model.LogicalParameters);
            Assert.Equal("@p1", logical.Name);
            Assert.Equal("Inventory", logical.Table);
            Assert.Equal("Quantity", logical.Column);
            Assert.Equal(typeof(int), logical.SemanticType);
            Assert.Same(TypePromotions.StringToInt32, logical.PromotionRule);

            DatabaseQuery translated = new SqliteQueryTranslator().Translate(model);
            LogicalParameter transported = Assert.Single(translated.LogicalParameters);
            Assert.Equal(logical.Name, transported.Name);
            Assert.Equal(logical.Table, transported.Table);
            Assert.Equal(logical.Column, transported.Column);
            Assert.Same(logical.PromotionRule, transported.PromotionRule);
        }

        [Fact]
        public void TypeAdaptationEventArgs_PublicSurfaceContainsNoValueOrCommandPayload()
        {
            string[] publicProperties = typeof(TypeAdaptationEventArgs)
                .GetProperties()
                .Select(property => property.Name)
                .ToArray();

            Assert.DoesNotContain("Value", publicProperties);
            Assert.DoesNotContain("ConvertedValue", publicProperties);
            Assert.DoesNotContain("Sql", publicProperties);
            Assert.DoesNotContain("Parameters", publicProperties);
            Assert.DoesNotContain("ConnectionString", publicProperties);
            Assert.DoesNotContain("InnerException", publicProperties);
        }

        private static QueryBuilder QuantityBuilder(string value)
        {
            return new QueryBuilder()
                .InsertInto("Inventory")
                .Value("Quantity", value);
        }

        private static QueryBuilder DateTimeOffsetBuilder(
            Action<LogicalParameterOptions> configure)
        {
            DateTimeOffset value = new DateTimeOffset(2026, 8, 27, 10, 30, 0, TimeSpan.FromHours(-3));
            QueryBuilder builder = new QueryBuilder().InsertInto("Inventory");
            return configure == null
                ? builder.Value("OccurredAt", value)
                : builder.Value("OccurredAt", value, configure);
        }

        private static FakeNonQueryConnectionFactory CreateFactory()
        {
            return new FakeNonQueryConnectionFactory(
                () => new FakeNonQueryCommand { Result = 1 });
        }

        private static FakeNonQueryConnectionFactory CreateSchemaFactory(Type columnType)
        {
            return new FakeNonQueryConnectionFactory(
                () => new FakeNonQueryCommand { Result = 1 },
                schemaFactory: (collection, restrictions) => CreateSchema(collection, columnType));
        }

        private static DataTable CreateSchema(string collectionName, Type columnType)
        {
            if (string.Equals(collectionName, "DataTypes", StringComparison.OrdinalIgnoreCase))
            {
                DataTable types = new DataTable();
                types.Columns.Add("TYPE_NAME", typeof(string));
                types.Columns.Add("DATA_TYPE", typeof(Type));
                types.Columns.Add("PROVIDER_DB_TYPE", typeof(string));
                types.Rows.Add(columnType.Name, columnType, columnType.Name);
                return types;
            }

            DataTable columns = new DataTable();
            columns.Columns.Add("TABLE_NAME", typeof(string));
            columns.Columns.Add("COLUMN_NAME", typeof(string));
            columns.Columns.Add("TYPE_NAME", typeof(string));
            columns.Columns.Add("DATA_TYPE", typeof(Type));
            columns.Rows.Add("Inventory", "Quantity", columnType.Name, columnType);
            columns.Rows.Add("Inventory", "OccurredAt", columnType.Name, columnType);
            return columns;
        }

        private static DataTable CreateOleDbCodeSchema(string collectionName, int providerType)
        {
            if (string.Equals(collectionName, "DataTypes", StringComparison.OrdinalIgnoreCase))
            {
                DataTable types = new DataTable();
                types.Columns.Add("TYPE_NAME", typeof(string));
                types.Columns.Add("DATA_TYPE", typeof(object));
                types.Columns.Add("PROVIDER_DB_TYPE", typeof(object));
                return types;
            }

            DataTable columns = new DataTable();
            columns.Columns.Add("TABLE_NAME", typeof(string));
            columns.Columns.Add("COLUMN_NAME", typeof(string));
            columns.Columns.Add("TYPE_NAME", typeof(string));
            columns.Columns.Add("DATA_TYPE", typeof(object));
            columns.Rows.Add("Inventory", "Quantity", "provider-code-only", providerType);
            return columns;
        }

        private static DataTable CreateUnknownSchema(string collectionName)
        {
            if (string.Equals(collectionName, "DataTypes", StringComparison.OrdinalIgnoreCase))
            {
                DataTable types = new DataTable();
                types.Columns.Add("TYPE_NAME", typeof(string));
                types.Columns.Add("DATA_TYPE", typeof(object));
                types.Columns.Add("PROVIDER_DB_TYPE", typeof(object));
                return types;
            }

            DataTable columns = new DataTable();
            columns.Columns.Add("TABLE_NAME", typeof(string));
            columns.Columns.Add("COLUMN_NAME", typeof(string));
            columns.Columns.Add("TYPE_NAME", typeof(string));
            columns.Columns.Add("DATA_TYPE", typeof(object));
            columns.Rows.Add("Inventory", "Quantity", "unknown_provider_type", 9999);
            return columns;
        }

        private static QueryExecutionContext CreateContext(
            FakeNonQueryConnectionFactory factory,
            DatabaseGatewayOptions options = null)
        {
            return new QueryExecutionContext(factory, new SqliteQueryTranslator(), options);
        }

        private static QueryExecutionContext CreateAccessContext(
            FakeNonQueryConnectionFactory factory,
            DatabaseGatewayOptions options = null)
        {
            return new QueryExecutionContext(factory, new AccessQueryTranslator(), options);
        }

        private static object ParameterValue(FakeNonQueryCommand command, string name)
        {
            return command.Parameters
                .Cast<DbParameter>()
                .Single(parameter => parameter.ParameterName == name)
                .Value;
        }

        private static object[] ParameterValues(FakeNonQueryCommand command)
        {
            return command.Parameters
                .Cast<DbParameter>()
                .Select(parameter => parameter.Value)
                .ToArray();
        }

        private sealed class RepeatingParameterTranslator : IDbQueryTranslator
        {
            public DatabaseQuery Translate(QueryModel model)
            {
                string sql = model.Sql.Replace(
                    "Quantity = @p2",
                    "Quantity = @p2, PreviousQuantity = @p2");
                return DatabaseQuery.FromLogicalParameters(
                    sql,
                    model.LogicalParameters,
                    model.CommandPolicy);
            }
        }

        private sealed class UnknownTranslator : IDbQueryTranslator
        {
            public DatabaseQuery Translate(QueryModel model)
            {
                return DatabaseQuery.FromLogicalParameters(
                    model.Sql,
                    model.LogicalParameters,
                    model.CommandPolicy);
            }
        }
    }
}
