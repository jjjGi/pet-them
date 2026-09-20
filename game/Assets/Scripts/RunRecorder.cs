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
            // comparable to another run that started with the same pair. The twist rewrites the
            // numbers the run is played with, so it has to be on that list too -- configJson below
            // already holds the twisted values, and this says which twist produced them.
            public string weapon, pet, twist;
            public string runId, startedUtc, buildVersion, platform, unityVersion, configJson;
            public int seed;
            public float fixedStep = CombatWorld.StepSeconds;
        }
        private StreamWriter writer;
        public string FilePath { get; }

        /// <param name="id">
        /// The run's identity, made by the caller rather than here. The upload queue needs the
        /// same id, and a run whose log file could not be opened still has to reach the server --
        /// so the two cannot share an id that only exists once a file has been created.
        /// </param>
        public RunRecorder(CombatWorld world, string id)
        {
            string directory = Path.Combine(Application.persistentDataPath, "runs");
            Directory.CreateDirectory(directory);
            FilePath = Path.Combine(directory, id + ".jsonl");
            writer = new StreamWriter(FilePath, false, new System.Text.UTF8Encoding(false));
            writer.WriteLine(JsonUtility.ToJson(new Header {
                runId = id, startedUtc = DateTime.UtcNow.ToString("O"), seed = world.Seed,
                buildVersion = Application.version, platform = Application.platform.ToString(),
                progressionEnabled = world.ProgressionEnabled, weapon = world.Weapon.ToString(),
                pet = world.Pet.ToString(), twist = world.Twist.ToString(),
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
