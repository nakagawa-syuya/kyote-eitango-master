using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 同期マネージャー
/// - オフライン時の回答データを未送信キューに保存
/// - ネット復帰時に一括送信
/// - アプリ起動時に未送信データを自動送信
/// </summary>
public class SyncManager : MonoBehaviour
{
    public static SyncManager Instance { get; private set; }

    private List<ApiManager.AnswerRequest> pendingQueue = new List<ApiManager.AnswerRequest>();
    private bool isSyncing = false;
    private float syncRetryInterval = 30f; // 再試行間隔（秒）

    private const string PENDING_KEY = "PendingSync";

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        // 未送信データを読み込み
        LoadPendingQueue();

        // 定期的に同期を試みる
        StartCoroutine(AutoSyncLoop());
    }

    // ═══════════════════════════════════════════
    //  外部から呼び出すメソッド
    // ═══════════════════════════════════════════

    /// <summary>
    /// 回答データの送信を試みる。失敗したらキューに追加。
    /// </summary>
    public void SendOrQueue(ApiManager.AnswerRequest req)
    {
        var api = ApiManager.Instance;
        var reachability = Application.internetReachability;

        Debug.Log($"[Sync] SendOrQueue: api={api != null} userId={api?.GetUserId()} net={reachability} qid={req.question_id}");

        // オフラインまたはAPI未準備 → キューに追加
        if (api == null || api.GetUserId() <= 0 || reachability == NetworkReachability.NotReachable)
        {
            Debug.Log($"[Sync] オフラインまたはAPI未準備 → キュー追加");
            AddToQueue(req);
            return;
        }

        // オンライン → 送信を試みる
        api.SendAnswer(req, success =>
        {
            if (!success)
            {
                Debug.LogWarning("[Sync] 送信失敗 → キューに追加");
                AddToQueue(req);
            }
            else
            {
                Debug.Log($"[Sync] 送信成功: qid={req.question_id}");
            }
        });
    }

    /// <summary>未送信データの件数</summary>
    public int GetPendingCount()
    {
        return pendingQueue.Count;
    }

    /// <summary>手動で同期を実行</summary>
    public void SyncNow()
    {
        if (!isSyncing)
            StartCoroutine(ProcessQueue());
    }

    // ═══════════════════════════════════════════
    //  キュー管理
    // ═══════════════════════════════════════════

    private void AddToQueue(ApiManager.AnswerRequest req)
    {
        pendingQueue.Add(req);
        SavePendingQueue();
        Debug.Log($"[Sync] キュー追加: qid={req.question_id} 残り{pendingQueue.Count}件");
    }

    // ═══════════════════════════════════════════
    //  自動同期ループ
    // ═══════════════════════════════════════════

    private IEnumerator AutoSyncLoop()
    {
        // 起動後少し待ってから初回同期
        yield return new WaitForSeconds(3f);

        while (true)
        {
            if (pendingQueue.Count > 0 && !isSyncing)
            {
                if (Application.internetReachability != NetworkReachability.NotReachable)
                {
                    yield return StartCoroutine(ProcessQueue());
                }
            }
            yield return new WaitForSeconds(syncRetryInterval);
        }
    }

    // ═══════════════════════════════════════════
    //  キュー処理（一括送信）
    // ═══════════════════════════════════════════

    private IEnumerator ProcessQueue()
    {
        if (pendingQueue.Count == 0) yield break;

        var api = ApiManager.Instance;
        if (api == null || api.GetUserId() <= 0) yield break;

        isSyncing = true;
        Debug.Log($"[Sync] 同期開始: {pendingQueue.Count}件");

        // コピーして処理（処理中にキューが変更される可能性があるため）
        var processing = new List<ApiManager.AnswerRequest>(pendingQueue);
        var failed = new List<ApiManager.AnswerRequest>();

        foreach (var req in processing)
        {
            // user_idを最新に更新
            req.user_id = api.GetUserId();

            bool done = false;
            bool success = false;

            api.SendAnswer(req, result =>
            {
                success = result;
                done = true;
            });

            // 完了待ち（最大10秒）
            float waited = 0f;
            while (!done && waited < 10f)
            {
                yield return new WaitForSeconds(0.1f);
                waited += 0.1f;
            }

            if (!done || !success)
            {
                Debug.LogWarning($"[Sync] 同期失敗: qid={req.question_id}");
                failed.Add(req);
                // 1件失敗したら残りも後回し
                break;
            }
            else
            {
                Debug.Log($"[Sync] 同期成功: qid={req.question_id}");
            }

            // サーバー負荷を避けるため少し待つ
            yield return new WaitForSeconds(0.1f);
        }

        // 成功分をキューから除去、失敗分は残す
        pendingQueue.Clear();
        pendingQueue.AddRange(failed);

        // 処理できなかった残りも追加
        int processedIndex = processing.IndexOf(failed.Count > 0 ? failed[0] : null);
        if (processedIndex >= 0)
        {
            for (int i = processedIndex + 1; i < processing.Count; i++)
                pendingQueue.Add(processing[i]);
        }

        SavePendingQueue();
        isSyncing = false;

        Debug.Log($"[Sync] 同期完了: 残り{pendingQueue.Count}件");
    }

    // ═══════════════════════════════════════════
    //  永続化（PlayerPrefs）
    // ═══════════════════════════════════════════

    [Serializable]
    private class PendingData
    {
        public List<ApiManager.AnswerRequest> items;
    }

    private void SavePendingQueue()
    {
        var data = new PendingData { items = pendingQueue };
        string json = JsonUtility.ToJson(data);
        PlayerPrefs.SetString(PENDING_KEY, json);
        PlayerPrefs.Save();
    }

    private void LoadPendingQueue()
    {
        string json = PlayerPrefs.GetString(PENDING_KEY, "");
        if (string.IsNullOrEmpty(json))
        {
            pendingQueue = new List<ApiManager.AnswerRequest>();
            return;
        }

        try
        {
            var data = JsonUtility.FromJson<PendingData>(json);
            pendingQueue = data.items ?? new List<ApiManager.AnswerRequest>();
            Debug.Log($"[Sync] 未送信データ読込: {pendingQueue.Count}件");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Sync] 読込エラー: {e.Message}");
            pendingQueue = new List<ApiManager.AnswerRequest>();
        }
    }

    private void OnApplicationPause(bool pause)
    {
        if (pause)
        {
            // バックグラウンドに行く時に保存
            SavePendingQueue();
        }
        else
        {
            // フォアグラウンドに戻った時、少し待ってから同期
            if (pendingQueue.Count > 0 && !isSyncing)
                StartCoroutine(DelayedSync());
        }
    }

    private IEnumerator DelayedSync()
    {
        yield return new WaitForSeconds(2f);

        if (Application.internetReachability != NetworkReachability.NotReachable
            && pendingQueue.Count > 0 && !isSyncing)
        {
            yield return StartCoroutine(ProcessQueue());
        }
    }
}
