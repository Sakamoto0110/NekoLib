#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using NekoLib.Data.RuntimeTests.SqlServer.Support;

namespace NekoLib.Data.RuntimeTests.SqlServer.Container
{
    /// <summary>
    /// The versioned description of the container this scenario's evidence is
    /// about, loaded from <c>container.json</c> next to the executable.
    /// <para/>
    /// It is a description, not a recipe: nothing here creates or modifies a
    /// container. Its purpose is to make the claim checkable — a result that
    /// says "SQL Server 2022 CU26" is worth nothing unless the run verified the
    /// image it actually connected to.
    /// </summary>
    internal sealed class PinnedContainerDefinition
    {
        public string ContainerName = "nekolib-sqlserver";
        public string Image = string.Empty;
        public string ImageDigest = string.Empty;
        public string ImageArchitecture = string.Empty;
        public string ImageOs = string.Empty;
        public int HostPort = 1433;
        public string RequiredHostIp = "127.0.0.1";
        public string PasswordVariable = "NEKOLIB_SQLSERVER_PASSWORD";
        public string LoginUser = "sa";
        public string ScenarioDatabasePrefix = "NekoLibE4Sql_";
        public string SourcePath = string.Empty;

        public static PinnedContainerDefinition Load()
        {
            string path = Path.Combine(AppContext.BaseDirectory, "container.json");
            if (!File.Exists(path))
            {
                throw new FileNotFoundException(
                    "The pinned container definition was not copied next to the executable. " +
                    "Rebuild the scenario project.",
                    path);
            }

            Dictionary<string, object?> map = JsonParser.AsObject(
                JsonParser.Parse(File.ReadAllText(path)),
                "container.json");

            PinnedContainerDefinition definition = new PinnedContainerDefinition
            {
                SourcePath = path,
                ContainerName = JsonParser.RequireString(map, "containerName"),
                Image = JsonParser.RequireString(map, "image"),
                ImageDigest = JsonParser.OptionalString(map, "imageDigest") ?? string.Empty,
                ImageArchitecture = JsonParser.OptionalString(map, "imageArchitecture") ?? string.Empty,
                ImageOs = JsonParser.OptionalString(map, "imageOs") ?? string.Empty,
                HostPort = (int)JsonParser.RequireInt(map, "hostPort"),
                RequiredHostIp = JsonParser.OptionalString(map, "requiredHostIp") ?? "127.0.0.1",
                PasswordVariable = JsonParser.RequireString(map, "passwordVariable"),
                LoginUser = JsonParser.RequireString(map, "loginUser"),
                ScenarioDatabasePrefix = JsonParser.RequireString(map, "scenarioDatabasePrefix")
            };

            return definition;
        }
    }
}
