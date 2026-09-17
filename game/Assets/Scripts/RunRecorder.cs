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
            public string type = "run_start", schemaVersion = "2", source = "human";
            public bool progressionEnabled;
            public string progressionVersion = "kill-xp-1";
            // The weapon and the pet decide which upgrades can appear, so a run is only
            // comparable to another run that started with the same pair.
            public string weapon, pet;
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
                progressionEnabled = world.ProgressionEnabled, weapon = world.Weapon.ToString(),
                pet = world.Pet.ToString(),
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
