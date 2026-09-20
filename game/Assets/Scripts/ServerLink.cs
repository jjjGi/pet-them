using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using PetThem.Combat;

namespace PetThem.Game
{
    /// <summary>
    /// Carries finished runs to the server, whenever that becomes possible.
    /// </summary>
    /// <remarks>
    /// Nothing in the game waits on this. A run ends, its coins are banked locally, and a copy
    /// goes in the outbox; whether it ever reaches a server is a separate question with a separate
    /// answer, possibly days later on a different network. The game stays playable with no
    /// connection and with no server configured at all, which is the whole reason the outbox is a
    /// file rather than a variable.
    ///
    /// The local profile is still the one the game plays from. The server holds the same numbers
    /// but has no storage or sign-in yet, so making it authoritative now would mean losing a
    /// player's coins every time the process restarts. This is the half that can be built without
    /// that being true, and the order matters: by the time authority moves, the uploads it depends
    /// on will already have been running.
    ///
    /// Retrying is safe to do freely because the server pays a given runId once. The stubbornness
    /// here and the idempotency there are one design, split across two machines.
    /// </remarks>
    public sealed class ServerLink : MonoBehaviour
    {
        /// <summary>Where the server is, or empty to keep everything local.</summary>
        /// <remarks>
        /// Empty by default on purpose: a build with no server configured must behave exactly like
        /// the game did before any of this existed, rather than spending the first seconds of every
        /// run failing to reach localhost.
        /// </remarks>
        public const string BaseUrlKey = "server.baseUrl";
        private const string TokenKey = "server.token";
        private const int TimeoutSeconds = 10;

        private RunOutbox outbox = new RunOutbox();
        private string baseUrl = "", token = "";
        private bool sending;
        private string fileError = "", lastSend = "";

        /// <summary>Runs waiting to go out. Shown on the settings screen.</summary>
        public int Pending => outbox.Count;

        /// <summary>True when a server has been configured at all.</summary>
        public bool Configured => baseUrl.Length > 0;

        /// <summary>The address the game is sending to, or empty when it is sending nowhere.</summary>
        public string BaseUrl => baseUrl;

        /// <summary>
        /// Points the game at a server, or at none when given nothing.
        /// </summary>
        /// <remarks>
        /// Typed into the settings screen rather than baked into the build, because while this is
        /// being built the address keeps changing: localhost from the editor, the PC's address on
        /// the network from a phone. A shipped game would carry its own and never ask.
        ///
        /// Queued runs are not touched. They were finished under whatever address was set at the
        /// time, but a run belongs to the player, not to a server, and the next attempt simply
        /// goes wherever the game is now pointed.
        /// </remarks>
        public void UseServer(string url)
        {
            baseUrl = (url ?? "").Trim().TrimEnd('/');
            PlayerPrefs.SetString(BaseUrlKey, baseUrl);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// How the last upload attempt went, in words, or empty before there has been one.
        /// </summary>
        /// <remarks>
        /// Without this the settings screen shows a queue that does not move and no reason why,
        /// which is the same failure as a screen full of empty boxes: something is wrong and the
        /// only person who can act on it has been told nothing. Whatever went wrong, say it.
        /// </remarks>
        public string LastSend => lastSend;

        /// <summary>The last thing that went wrong, for the settings screen. Empty when nothing has.</summary>
        public string Trouble => fileError.Length > 0 ? fileError
            : outbox.discarded.Length > 0 ? outbox.discarded[outbox.discarded.Length - 1] : "";

        private string Path => System.IO.Path.Combine(Application.persistentDataPath, "outbox.json");

        private void Awake()
        {
            baseUrl = PlayerPrefs.GetString(BaseUrlKey, "").Trim().TrimEnd('/');
            token = PlayerPrefs.GetString(TokenKey, "");
            Load();
        }

        /// <summary>
        /// Files a finished run for delivery. Returns having touched only a local file.
        /// </summary>
        public void Record(CombatWorld world, string runId)
        {
            if (world == null || string.IsNullOrEmpty(runId)) return;
            bool added = outbox.Enqueue(new PendingRun
            {
                runId = runId,
                seed = world.Seed,
                weapon = world.Weapon.ToString(),
                pet = world.Pet.ToString(),
                kills = world.Kills,
                seconds = world.Time,
                bossDefeated = world.BossDefeated,
            }, Now);
            if (added) Save();
        }

        private void Update()
        {
            if (sending || !Configured || outbox.Due(Now) == null) return;
            StartCoroutine(SendNext());
        }

        private IEnumerator SendNext()
        {
            sending = true;
            PendingRun run = outbox.Due(Now);
            if (run != null)
            {
                if (token.Length == 0) yield return OpenSession();
                if (token.Length > 0) yield return Send(run);
                else outbox.Record(run.runId, UploadVerdict.Retry, Now);
                Save();
            }
            sending = false;
        }

        private IEnumerator OpenSession()
        {
            using UnityWebRequest request = Post("/v1/session", "{}");
            yield return request.SendWebRequest();
            if (!Succeeded(request)) { lastSend = Describe(request); yield break; }
            var session = JsonUtility.FromJson<SessionReply>(request.downloadHandler.text);
            if (session == null || string.IsNullOrEmpty(session.token))
            { lastSend = Texts.SendNoSession; yield break; }
            token = session.token;
            PlayerPrefs.SetString(TokenKey, token);
            PlayerPrefs.Save();
        }

        private IEnumerator Send(PendingRun run)
        {
            using UnityWebRequest request = Post("/v1/runs", JsonUtility.ToJson(run));
            request.SetRequestHeader("Authorization", "Bearer " + token);
            yield return request.SendWebRequest();

            bool broken = request.result == UnityWebRequest.Result.ConnectionError
                          || request.result == UnityWebRequest.Result.DataProcessingError;
            UploadVerdict verdict = RunOutbox.VerdictFor(request.responseCode, broken);
            lastSend = verdict == UploadVerdict.Done ? Texts.SendOk : Describe(request);
            if (verdict == UploadVerdict.Reauthenticate)
            {
                // The run is fine and the session is not. Drop the token so the next attempt opens
                // a new one; the run itself just waits its turn again.
                token = "";
                PlayerPrefs.DeleteKey(TokenKey);
                PlayerPrefs.Save();
            }
            outbox.Record(run.runId, verdict, Now);
        }

        private UnityWebRequest Post(string path, string body)
        {
            var request = new UnityWebRequest(baseUrl + path, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(body)),
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = TimeoutSeconds,
            };
            request.SetRequestHeader("Content-Type", "application/json");
            return request;
        }

        private static bool Succeeded(UnityWebRequest request) =>
            request.result == UnityWebRequest.Result.Success;

        /// <summary>
        /// What happened, in terms someone standing in front of the phone can act on.
        /// </summary>
        /// <remarks>
        /// A reply that never arrived and a reply that said no are different problems with
        /// different answers -- check the address and the network, or look at the server -- so
        /// they do not get the same message.
        /// </remarks>
        private static string Describe(UnityWebRequest request) =>
            request.responseCode > 0
                ? Texts.SendRefused(request.responseCode)
                : Texts.SendUnreachable(request.error ?? "");

        private static double Now => (DateTime.UtcNow - DateTime.UnixEpoch).TotalSeconds;

        /// <summary>
        /// Reads the queue back, repairing anything a half-finished write left behind.
        /// </summary>
        /// <remarks>
        /// A queue that cannot be read is emptied rather than allowed to throw on every frame.
        /// Losing the record of some runs is bad; a game that will not start is worse, and the
        /// player would have no way to tell the difference from the outside.
        /// </remarks>
        private void Load()
        {
            try
            {
                if (!File.Exists(Path)) return;
                outbox = JsonUtility.FromJson<RunOutbox>(File.ReadAllText(Path)) ?? new RunOutbox();
                var repairs = outbox.Normalize();
                if (repairs.Count > 0) Debug.LogWarning($"Outbox repaired: {string.Join("; ", repairs)}");
            }
            catch (Exception ex)
            {
                outbox = new RunOutbox();
                fileError = Texts.OutboxUnavailable(ex.Message);
                Debug.LogWarning(fileError);
            }
        }

        /// <summary>Writes to a temporary file and swaps, so a crash mid-write keeps the old queue.</summary>
        private void Save()
        {
            try
            {
                string temporary = Path + ".tmp";
                File.WriteAllText(temporary, JsonUtility.ToJson(outbox));
                File.Copy(temporary, Path, true);
                File.Delete(temporary);
                fileError = "";
            }
            catch (Exception ex)
            {
                fileError = Texts.OutboxUnavailable(ex.Message);
                Debug.LogWarning(fileError);
            }
        }

        [Serializable]
        private sealed class SessionReply
        {
            public string playerId = "", token = "";
        }
    }
}
