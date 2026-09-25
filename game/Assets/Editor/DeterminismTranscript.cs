using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using PetThem.Combat;
using PetThem.Game;

namespace PetThem.Editor
{
    /// <summary>
    /// Writes Unity's transcript of the fixed fight, to be compared against the .NET one.
    /// </summary>
    /// <remarks>
    /// Replay validation rests on the same seed and the same inputs producing the same run
    /// wherever it is played. That has never been measured, and the combat core calls Math.Sin and
    /// Math.Cos in eight places that decide where things spawn -- functions IEEE-754 does not
    /// require any two runtimes to agree on to the last bit. One bit in a spawn angle is a
    /// different fight a minute later.
    ///
    /// Runs in batch mode, so measuring costs a command rather than a person sitting through three
    /// minutes of play. It does not enter play mode and touches nothing in the scene: CombatWorld
    /// needs no engine, which is the whole reason any of this is possible.
    ///
    /// What this compares is the editor's runtime against the server's. It does not reach a phone,
    /// where the answer could still differ -- IL2CPP compiles to C++ and ARM64 is not x64. Two of
    /// the three runtimes is what can be had without a device in hand, and a difference here would
    /// settle the question without one.
    /// </remarks>
    public static class DeterminismTranscript
    {
        private const string Output = "Builds/determinism-unity.txt";

        [MenuItem("PET THEM/Write determinism transcript")]
        public static void Write()
        {
            var asset = Resources.Load<TextAsset>("balance-default");
            if (asset == null) throw new InvalidOperationException("Missing balance-default.json.");
            BalanceConfig config = JsonUtility.FromJson<BalanceConfig>(asset.text);
            // The probe builds the world, so the two runtimes cannot be set up differently.
            var world = DeterminismProbe.NewWorld(config);

            Directory.CreateDirectory("Builds");
            File.WriteAllText(Output, DeterminismProbe.Transcribe(world));
            File.WriteAllText("Builds/outcomes-unity.txt", DeterminismProbe.Outcomes(config, 40));
            Debug.Log($"Determinism transcript written to {Output} " +
                $"({new FileInfo(Output).Length} bytes, Unity {Application.unityVersion}).");
        }
    }
}
