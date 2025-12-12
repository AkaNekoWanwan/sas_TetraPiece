using UnityEngine;
using UnityEngine.UI;
using AkanekoLib;
using UnityEngine.Events;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine.SceneManagement;

public class DailyMissionManager : MonoBehaviour
{
    public CustomButton _showDailyMissionButton;
    public GameObject _DailyMissionWindowParentBack;
    public GameObject _DailyMissionWindow;
    public CustomButton _closeButton;
    public List<DailyButton> _dailyButtons;
    public CustomButton _playButton;
    public CustomButton _rewardPlayButton;
    private DailyButton _selectedButton;
    private int _today = -1;
    private int _lastDay = -1;
    public bool _isDailyStage = false;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        int month = System.DateTime.Now.Month;
        _today = System.DateTime.Now.Day;
        int weekday = (int)System.DateTime.Now.DayOfWeek; // 日曜日=0、月曜日=1、...土曜日=6
        int firstDayWeekday = (weekday - (_today - 1) % 7 + 7) % 7; // その月の1日の曜日を計算
        // その月の最終日を計算
        _lastDay = System.DateTime.DaysInMonth(System.DateTime.Now.Year, month);

        int saveMonth = PlayerPrefs.GetInt("DailyMissionMonth", 0);
        if(saveMonth != month)
        {
            // 月が変わっていたらセーブデータをリセット
            PlayerPrefs.SetInt("DailyClearData", 0);
            PlayerPrefs.SetInt("DailyMissionMonth", month);
        }

        int unClearLastDay = GetUnClearLastDay(_today);

        for(int i = 0; i < _dailyButtons.Count; i++)
        {
            // その月の１日が日曜日なら1始まり、月曜日なら0始まり、火曜日なら-1始まりにする
            int day = i + 1; // とりあえず1始まりで
            day -= firstDayWeekday;

            bool isSelected = (day == unClearLastDay);
            _dailyButtons[i].UpdateView(day, _today, _lastDay, isSelected);
            if(isSelected)
            {
                _selectedButton = _dailyButtons[i];
            }
            _dailyButtons[i].onClickDailyButton += (selectedDay) =>
            {
                SelectButton(selectedDay);
            };
        }
        UpdateViewPlayButton(_selectedButton._day);

        _playButton.onClick += () =>
        {
            PlayStage();
        };
        _rewardPlayButton.onClick += () =>
        {
            RewardedAdManager.instance.ShowReward(() =>
            {   
                PlayStage();
            },
            () =>
            {
                // リワード視聴失敗時の処理（任意）
            });
        };
        int currentDailyStage = PlayerPrefs.GetInt("DailyStage", -1);
        // _showDailyMissionButton.gameObject.SetActive(!_isDailyStage);

        _showDailyMissionButton.onClick += () =>
        {
            ShowDailyMissionView();
        };
        _closeButton.onClick += () =>
        {
            _DailyMissionWindow.transform.DOScale(0f, 0.15f).SetEase(Ease.Linear).SetLink(_DailyMissionWindow).OnComplete(() =>
            {
                _DailyMissionWindowParentBack.SetActive(false);
            });
        };

        if(PlayerPrefs.GetInt("beforeDailyClear", 0) == 1)
        {
            // 直前にデイリーミッションをクリアしていたらウィンドウを開いた状態にする
            ShowDailyMissionView();
            PlayerPrefs.SetInt("beforeDailyClear", 0);
        }
        else
            _DailyMissionWindowParentBack.SetActive(false);
    }

    private void ShowDailyMissionView()
    {
        SelectButton(GetUnClearLastDay(_today));
        _DailyMissionWindowParentBack.SetActive(true);
        _DailyMissionWindow.SetActive(true);
        _DailyMissionWindow.transform.localScale = Vector3.zero;
        _DailyMissionWindow.transform.DOScale(1.85f, 0.2f).SetEase(Ease.OutBack).SetLink(_DailyMissionWindow);
    }

    // 未クリアの最終日を取得
    private int GetUnClearLastDay(int _today)
    {
        int clearSaveData = PlayerPrefs.GetInt("DailyClearData", 0);
        for(int day = _today; day >= 1; day--)
        {
            if((clearSaveData & (1 << (day - 1))) == 0)
            {
                return day;
            }
        }
        return 0;
    }

    private void SelectButton(int day)
    {
        if(_selectedButton != null)
        {
            _selectedButton.UpdateView(_selectedButton._day, _today, _lastDay, false);
        }
        _selectedButton = _dailyButtons.First(b => b._day == day);
        _selectedButton.UpdateView(_selectedButton._day, _today, _lastDay, true);
        UpdateViewPlayButton(day);
    }

    private void UpdateViewPlayButton(int selectedDay)
    {
        if(selectedDay == _today)
        {
            // 今日のミッションをプレイ
            _playButton.gameObject.SetActive(true);
            _rewardPlayButton.gameObject.SetActive(false);
        }
        else
        {
            // 過去日のミッションをリワード視聴でプレイ
            _playButton.gameObject.SetActive(false);
            _rewardPlayButton.gameObject.SetActive(true);
        }
    }

    private void PlayStage()
    {
        PlayerPrefs.SetInt("DailyStage", _selectedButton._day);
        PlayerPrefs.Save();
        // HomeManager.Instance.FedeGoStage();
        GameDataManager.DailyInitMoveCount = -1;
        GameDataManager.IsHome = false;
        FadeManager.Instance.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name, 0.25f);
    }
    private void BackStage()
    {
        PlayerPrefs.SetInt("DailyStage", -1);
        PlayerPrefs.Save();
        // HomeManager.Instance.FedeGoHome();
        GameDataManager.IsHome = true;
        FadeManager.Instance.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name, 0.25f);
    }
}
