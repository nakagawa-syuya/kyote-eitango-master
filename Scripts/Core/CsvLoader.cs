using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// questionlist.csv を Resources から読み込み、
/// StudyManager.Initialize() へ渡す。
///
/// ■ 配置手順
///   1. Assets/Resources/ フォルダを作成（なければ）
///   2. questionlist.csv を Assets/Resources/ に配置
///      → Unity上では Assets/Resources/questionlist.csv
///   3. CsvLoader の Inspector で studyManager を接続
///
/// ■ 注意
///   Resources.Load("questionlist") で読み込むため、
///   ファイル名は拡張子なしで "questionlist" と一致させること。
/// </summary>
public class CsvLoader : MonoBehaviour
{
    [SerializeField] private StudyManager studyManager;

    private void Start()
    {
        LoadCsv();
    }

    private void LoadCsv()
    {
        // Resources から TextAsset として読み込み
        TextAsset csvFile = Resources.Load<TextAsset>("questionlist");

        if (csvFile == null)
        {
            Debug.LogError("[CsvLoader] CSV読込失敗: Assets/Resources/questionlist.csv が見つかりません");
            return;
        }

        string csv = csvFile.text;

        // BOM除去
        if (csv.Length > 0 && csv[0] == '\uFEFF')
            csv = csv.Substring(1);

        var questions = new List<StudyManager.Question>();
        string[] lines = csv.Split(new[] { "\r\n", "\n" }, System.StringSplitOptions.RemoveEmptyEntries);

        // 1行目はヘッダー → スキップ
        for (int i = 1; i < lines.Length; i++)
        {
            string[] cols = lines[i].Split(',');
            if (cols.Length < 5) continue;

            int id;
            if (!int.TryParse(cols[0].Trim(), out id)) continue;

            int level;
            if (!int.TryParse(cols[4].Trim(), out level)) level = 1;

            questions.Add(new StudyManager.Question
            {
                id           = id,
                title        = cols[1].Trim(),
                answer       = cols[2].Trim(),
                partOfSpeech = cols[3].Trim(),
                level        = level,
            });
        }

        Debug.Log($"[CsvLoader] {questions.Count}問 読込完了");
        studyManager.Initialize(questions);
    }
}
