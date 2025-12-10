using UnityEngine;
using UnityEditor;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;

/// <summary>
/// スプレッドシートからCSVをロードしてステージに適用させる  
/// <summary>
/// 

[System.Serializable]
public class StageData
{
    public string stageId;
    public string gridIds;
    public string gridXString;
    public string gridYString;
    public string pieceNumString;
    public string shapeTypeString;

    public int gridX;
    public int gridY;
    public int pieceNum;
    public ShapeType shapeType;
}

public class CSVLoader : MonoBehaviour
{
#if UNITY_EDITOR
    private const string SPREADSHEET_URL = "https://docs.google.com/spreadsheets/d/1xz2He2ydHgi1OdS-PIpgq10ybxWeYPfjUOmWiBefCJ0/export?format=csv";

    [Header("Imported Data")]
    [SerializeField, Tooltip("インポートデータ")] private List<StageData> _classList = new List<StageData>();
    [SerializeField, Tooltip("ステージクリエーター")] private StageCreator _stageCreator = null;

    // ボタンから呼び出す公開メソッド
    public void ImportDataFromSpreadsheet()
    {
        StartCoroutine(GetSpreadsheetData());
    }

    private IEnumerator GetSpreadsheetData()
    {
        using (UnityWebRequest www = UnityWebRequest.Get(SPREADSHEET_URL))
        {
            Debug.Log("スプレッドシートのダウンロードを開始...");

            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.ConnectionError || www.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError("ダウンロードエラー: " + www.error);
            }
            else
            {
                Debug.Log("ダウンロード完了！データのパースを開始します...");
                string csvData = www.downloadHandler.text;
                ParseCsvData(csvData);
                Generate();
                Debug.Log("ステージの設定が完了しました！");
            }
        }
    }

    private void ParseCsvData(string csvText)
    {
        _classList.Clear();
        string[] lines = csvText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        Debug.Log($"総行数: {lines.Length}");
        for (int i = 2; i < lines.Length; i++)
        {
            string line = lines[i];
            string[] values = line.Split(',');
            
            if(6 <= values.Length)
                Debug.Log($"行 {i + 1} のLength: {values.Length}, values[5]:{values[5]}");
            else
                Debug.Log($"行 {i + 1} のLength: {values.Length}");

            if (!ChkActiveData(values))
            {
                Debug.LogWarning($"行 {i + 1} のデータが不完全です。スキップします。");
                continue;
            }
            try
            {
                StageData data = new StageData();
                data.stageId = $"Stage_{i - 1}";
                data.gridXString = values[6];
                data.gridYString = values[7];
                data.pieceNumString = "-1";
                data.shapeTypeString = values[1];
                data.gridIds = values[5];

                int.TryParse(data.gridXString, out data.gridX);
                int.TryParse(data.gridYString, out data.gridY);
                int.TryParse(data.pieceNumString, out data.pieceNum);
                data.shapeType = ParseShapeType(data.shapeTypeString);

                _classList.Add(data);
                Debug.Log($"行 {i + 1} をパースしました: {data.ToString()}");
            }
            catch (Exception e)
            {
                Debug.LogError($"行 {i + 1} のパース中にエラーが発生しました: {e.Message}");
            }
        }
        Debug.Log($"全データのパースが完了しました。合計 {_classList.Count} クラスが作成されました。");
    }

    // データの生合成チェック
    private bool ChkActiveData(string[] values)
    {
        if(values.Length < 7)
            return false;
        if(string.IsNullOrEmpty(values[1]))
            return false;
        if(string.IsNullOrEmpty(values[5]))
        {
            if(string.IsNullOrEmpty(values[6]) || string.IsNullOrEmpty(values[7]))
                return false;
        }
        return true;
    }

    private ShapeType ParseShapeType(string value)
    {
        ShapeType ret;
        switch (value)
        {
            case "🔺":
                ret = ShapeType.Triangle;
                break;
            case "六":
            case "六角":
                ret = ShapeType.Hex;
                break;
            case "■":
            default:
                ret = ShapeType.Square;
                break;
        }
        Debug.Log($"ParseShapeType:{value}, {ret}");
        return ret;
    }

    private void Generate()
    {
        _stageCreator.SetStagePatamList(_classList);
    }
#endif
#if UNITY_EDITOR
    [CustomEditor(typeof(CSVLoader))]
    public class CSVLoaderEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            CSVLoader generator = (CSVLoader)target;
            if (GUILayout.Button("Load"))
            {
                generator.ImportDataFromSpreadsheet();
            }
            base.OnInspectorGUI();
        }
    }
#endif
}