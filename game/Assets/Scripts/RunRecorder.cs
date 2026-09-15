using System;
using System.IO;
using PetThem.Combat;
using UnityEngine;

namespace PetThem.Game
{
    public sealed class RunRecorder : IDisposable
    {
        [Serializable]
        private sealed class Header
        {
            public string type = "run_start", schemaVersion = "1", source = "human";
            public string runId, startedUtc, buildVersion, platform, unityVersion, configJson;
            public int seed;
            public float fixedStep = CombatWorld.StepSeconds;
        }
        private StreamWriter writer;
        public string FilePath { get; }
        public RunRecorder(CombatWorld world)
        {
            string id = DateTime.UtcNow.ToString("yyyyMMddTHHmmssfff") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            string directory = Path.Combine(Application.persistentDataPath, "runs");
            Directory.CreateDirectory(directory);
            FilePath = Path.Combine(directory, id + ".jsonl");
            writer = new StreamWriter(FilePath, false, new System.Text.UTF8Encoding(false));
            writer.WriteLine(JsonUtility.ToJson(new Header {
                runId = id, startedUtc = DateTime.UtcNow.ToString("O"), seed = world.Seed,
                buildVersion = Application.version, platform = Application.platform.ToString(),
                unityVersion = Application.unityVersion, configJson = JsonUtility.ToJson(world.GetConfig())
            }));
            writer.Flush();
        }
        public void Capture(CombatWorld world)
        {
            if (writer == null) return;
            foreach (CombatEvent record in world.Events) writer.WriteLine(JsonUtility.ToJson(record));
            if (world.Tick % 60 == 0 || world.State != RunState.Playing) writer.Flush();
        }
        public void Flush() { writer?.Flush(); }
        public void Dispose() { writer?.Dispose(); writer = null; }
    }
}
