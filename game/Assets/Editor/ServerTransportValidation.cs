using System;
using System.Reflection;
using PetThem.Game;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace PetThem.Editor
{
    /// <summary>Real Unity HTTP transport with synthetic data. Does not touch player saves or preferences.</summary>
    public static class ServerTransportValidation
    {
        private static UnityWebRequest request;
        private static GameObject fixture;
        private static ServerLink link;
        private static string token;
        private static int step;
        private static double deadline;
        private static readonly PendingRun Run = new PendingRun
        { runId = "unity-transport-" + Guid.NewGuid().ToString("N"), seed = 42, weapon = "Punch", pet = "Mochi", kills = 3, seconds = 20 };

        public static void Verify()
        {
            try
            {
                // An inactive edit-mode object never starts the gameplay upload loop.
                fixture = new GameObject("HTTP transport fixture");
                fixture.SetActive(false);
                link = fixture.AddComponent<ServerLink>();
                typeof(ServerLink).GetField("baseUrl", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(link, "http://127.0.0.1:5165");
                Begin("/v1/session", "{}");
                EditorApplication.update += Poll;
            }
            catch (Exception ex) { Finish(ex); }
        }

        private static void Begin(string path, string body)
        {
            request = (UnityWebRequest)typeof(ServerLink).GetMethod("Post", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(link, new object[] { path, body });
            if (!string.IsNullOrEmpty(token)) request.SetRequestHeader("Authorization", "Bearer " + token);
            deadline = EditorApplication.timeSinceStartup + 20;
            request.SendWebRequest();
        }

        private static void Poll()
        {
            try
            {
                if (!request.isDone)
                {
                    if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Unity HTTP fixture timed out.");
                    return;
                }
                if (request.result != UnityWebRequest.Result.Success || request.responseCode != 200)
                    throw new Exception("Unity HTTP fixture failed: " + request.responseCode + " " + request.error);
                string json = request.downloadHandler.text;
                request.Dispose(); request = null;
                if (step++ == 0)
                {
                    token = JsonUtility.FromJson<Session>(json).token;
                    if (string.IsNullOrEmpty(token)) throw new Exception("Session token missing.");
                    Begin("/v1/runs", JsonUtility.ToJson(Run));
                }
                else
                {
                    var receipt = JsonUtility.FromJson<Receipt>(json);
                    if (receipt.runId != Run.runId || receipt.alreadyCounted != (step == 3))
                        throw new Exception("Unexpected run receipt or duplicate payout.");
                    if (step == 2) Begin("/v1/runs", JsonUtility.ToJson(Run));
                    else Finish(null);
                }
            }
            catch (Exception ex) { Finish(ex); }
        }

        private static void Finish(Exception error)
        {
            EditorApplication.update -= Poll;
            request?.Dispose(); request = null;
            if (fixture != null) UnityEngine.Object.DestroyImmediate(fixture);
            if (error != null) Debug.LogException(error);
            else Debug.Log("UNITY HTTP VERIFIED: session 200, run 200, duplicate 200/alreadyCounted. Synthetic transport fixture; gameplay BankRun/Update and Android not tested.");
            EditorApplication.Exit(error == null ? 0 : 1);
        }

        [Serializable] private sealed class Session { public string token = ""; }
        [Serializable] private sealed class Receipt { public string runId = ""; public bool alreadyCounted = false; }
    }
}
