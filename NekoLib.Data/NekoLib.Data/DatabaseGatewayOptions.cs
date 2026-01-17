using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NekoLib.Data 
{
    /// <summary>
    /// Modos possíveis para retornos dinâmicos.
    /// </summary>
    [Flags]
    public enum DynamicMode
    {
        /// <summary>Usa Reflection.Emit (rápido, mas não funciona em AOT e não é unloadable).</summary>
        IL = 1,

        /// <summary>Usa ExpandoObject/Dictionary (sem Reflection.Emit, seguro para AOT).</summary>
        Expando = 2,

        /// <summary>Desabilita retornos dinâmicos (força DTO/Raw).</summary>
        Disabled = 4,
    }

    /// <summary>
    /// Opções globais para o DatabaseGateway (produção / kiosk-safe).
    /// </summary>
    public sealed class DatabaseGatewayOptions
    {
        /// <summary>
        /// Modo dinâmico padrão (recomendado: Expando em produção).
        /// </summary>
        public DynamicMode DynamicMode { get; set; } = DynamicMode.Expando;

        /// <summary>
        /// Limite máximo de "schemas" para modo IL. (Tipos emitidos não são unloadable).
        /// </summary>
        public int MaxDynamicSchemas { get; set; } = 64;

        /// <summary>
        /// Se true, ao estourar o limite ou quando IL não é suportado, lança exceção.
        /// Se false, faz fallback para Expando (se permitido).
        /// </summary>
        public bool FailOnDynamicSchemaLimit { get; set; } = true;

        /// <summary>
        /// Se true, permite fallback automático para Expando quando IL não é suportado (ex: AOT).
        /// </summary>
        public bool AllowExpandoFallback { get; set; } = true;

        /// <summary>
        /// Se true, limpa eventos em Dispose() do QueryExecutionContext (evita leaks por assinantes).
        /// </summary>
        public bool ClearEventsOnContextDispose { get; set; } = true;

        public void Validate()
        {
            if (MaxDynamicSchemas < 1) MaxDynamicSchemas = 1;
        }
    }
}
