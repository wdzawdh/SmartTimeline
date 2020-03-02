using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(SmartTimeline))]
public class SmartTimelineInspector : Editor
{
    private SmartTimeline editAnimator
    {
        get { return target as SmartTimeline; }
    }

    private const int progressLableWidth = 80;
    private float mTotalWidth = 300;
    private bool showTagField;

    private void OnEnable()
    {
        editAnimator.playEndCallback = OnPlayEnd;
        EditorApplication.update += Update;
        Refresh();
    }

    private void Awake()
    {
        if (!editAnimator.recordingState)
        {
            ResetState();
        }
    }

    private void OnPlayEnd()
    {
        Repaint();
    }

    private void Update()
    {
        if (editAnimator.isPlaying) Repaint();
        if (!Application.isPlaying)
        {
            editAnimator.Update();
        }
    }

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        ShowTagGroup();
        ShowSaveStateGroup();
        ShowCurrentGroup();
        ShowAddTrackBtnGroup();
        ShowTracks();
        ShowPlayBtnGroup();
        ShowSpeedSilder();
        ShowTotalProgressBar("Total");
        ShowProgressBars();
        if (GUI.changed)
        {
            Refresh();
            Undo.RecordObject(editAnimator, "SmartTimeline");
            EditorUtility.SetDirty(editAnimator);
            if (!Application.isPlaying)
            {
                EditorSceneManager.MarkSceneDirty(editAnimator.gameObject.scene);
            }
        }
    }

    private void ShowTagGroup()
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("ShowTag (显示Tag，用于获取Track)", GUILayout.Width(250));
        showTagField = EditorGUILayout.Toggle(showTagField);
        EditorGUILayout.EndHorizontal();
    }

    private void ShowSaveStateGroup()
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Recording State (录制初始显示和位置)", GUILayout.Width(250));
        editAnimator.recordingState = EditorGUILayout.Toggle(editAnimator.recordingState);
        if (GUILayout.Button("Save", GUILayout.Width(50)))
        {
            SaveState();
        }
        EditorGUILayout.EndHorizontal();
    }

    private void ShowCurrentGroup()
    {
        EditorGUILayout.BeginHorizontal();
        int currentGroup = editAnimator.currentGroup;
        GUILayout.Label("currentGroup (当前执行的分组)", GUILayout.Width(250));
        editAnimator.currentGroup = EditorGUILayout.Popup(currentGroup, editAnimator.groupIds);
        EditorGUILayout.EndHorizontal();
    }

    private void ShowAddTrackBtnGroup()
    {
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("AddActiveTrack"))
        {
            editAnimator.activeTracks.Add(new SmartTimeline.ActiveTrack());
        }
        if (GUILayout.Button("AddAnimationTrack"))
        {
            editAnimator.animTracks.Add(new SmartTimeline.AnimTrack());
        }
        if (GUILayout.Button("AddAudioTrack"))
        {
            editAnimator.audioTracks.Add(new SmartTimeline.AudioTrack());
        }
        EditorGUILayout.EndHorizontal();
    }

    private void ShowTracks()
    {
        int groupId = 0;
        foreach (SmartTimeline.BaseTrack baseTrack in editAnimator.allTracks)
        {
            if (baseTrack.groupIndex != groupId && baseTrack.groupIndex < editAnimator.groupIds.Length)
            {
                GUILayout.Label("GROUP(" + editAnimator.groupIds[baseTrack.groupIndex] + ")");
            }
            groupId = baseTrack.groupIndex;
            if (baseTrack is SmartTimeline.ActiveTrack)
            {
                SmartTimeline.ActiveTrack activeTrack = (SmartTimeline.ActiveTrack) baseTrack;
                ShowActiveTrack(activeTrack);
            }
            if (baseTrack is SmartTimeline.AnimTrack)
            {
                SmartTimeline.AnimTrack animTrack = (SmartTimeline.AnimTrack) baseTrack;
                ShowAnimTrack(animTrack);
            }
            if (baseTrack is SmartTimeline.AudioTrack)
            {
                SmartTimeline.AudioTrack audioTrack = (SmartTimeline.AudioTrack) baseTrack;
                ShowAudioTrack(audioTrack);
            }
        }
    }

    private void ShowActiveTrack(SmartTimeline.ActiveTrack activeTrack)
    {
        string[] groupIds = editAnimator.groupIds;
        if (activeTrack == null) return;
        int groupIndex = activeTrack.groupIndex;
        GameObject currentSelectObj = activeTrack.currentSelectObj;
        bool animTrackActive = activeTrack.active;
        EditorGUILayout.BeginHorizontal();
        ShowTagField(activeTrack);
        activeTrack.groupIndex = EditorGUILayout.Popup(groupIndex, groupIds, GUILayout.Width(50));
        activeTrack.currentSelectObj =
            EditorGUILayout.ObjectField(currentSelectObj, typeof(GameObject), true) as GameObject;
        activeTrack.active = EditorGUILayout.Toggle(animTrackActive, GUILayout.Width(50));
        activeTrack.duration = EditorGUILayout.DelayedFloatField(activeTrack.duration);
        if (GUILayout.Button("Delete", GUILayout.Width(50)))
        {
            editAnimator.activeTracks.Remove(activeTrack);
        }
        EditorGUILayout.EndHorizontal();
    }

    private void ShowAnimTrack(SmartTimeline.AnimTrack animTrack)
    {
        string[] groupIds = editAnimator.groupIds;
        if (animTrack == null) return;
        int groupIndex = animTrack.groupIndex;
        GameObject currentSelectObj = animTrack.currentSelectObj;
        AnimationClip animationClip = animTrack.currentAnimationClip;
        EditorGUILayout.BeginHorizontal();
        ShowTagField(animTrack);
        animTrack.groupIndex = EditorGUILayout.Popup(groupIndex, groupIds, GUILayout.Width(50));
        animTrack.currentSelectObj =
            EditorGUILayout.ObjectField(currentSelectObj, typeof(GameObject), true) as GameObject;
        animTrack.currentAnimationClip =
            EditorGUILayout.ObjectField(animationClip, typeof(AnimationClip), true) as AnimationClip;
        float duration = animTrack.duration;
        if (animTrack.currentAnimationClip != null && Math.Abs(duration) < 0.01f)
        {
            duration = animTrack.currentAnimationClip.length;
        }
        animTrack.duration = EditorGUILayout.DelayedFloatField(duration);
        if (GUILayout.Button("Delete", GUILayout.Width(50)))
        {
            editAnimator.animTracks.Remove(animTrack);
        }
        EditorGUILayout.EndHorizontal();
    }

    private void ShowAudioTrack(SmartTimeline.AudioTrack audioTrack)
    {
        string[] groupIds = editAnimator.groupIds;
        if (audioTrack == null) return;
        int groupIndex = audioTrack.groupIndex;
        GameObject currentSelectObj = audioTrack.currentSelectObj;
        AudioClip audioClip = audioTrack.audioClip;
        EditorGUILayout.BeginHorizontal();
        ShowTagField(audioTrack);
        audioTrack.groupIndex = EditorGUILayout.Popup(groupIndex, groupIds, GUILayout.Width(50));
        audioTrack.currentSelectObj =
            EditorGUILayout.ObjectField(currentSelectObj, typeof(GameObject), true) as GameObject;
        audioTrack.audioClip =
            EditorGUILayout.ObjectField(audioClip, typeof(AudioClip), true) as AudioClip;
        float duration = audioTrack.duration;
        if (audioTrack.audioClip != null && Math.Abs(duration) < 0.01f)
        {
            duration = audioTrack.audioClip.length;
        }
        audioTrack.duration = EditorGUILayout.DelayedFloatField(duration);
        if (GUILayout.Button("Delete", GUILayout.Width(50)))
        {
            editAnimator.audioTracks.Remove(audioTrack);
        }
        EditorGUILayout.EndHorizontal();
    }

    private void ShowPlayBtnGroup()
    {
        EditorGUILayout.Space();
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Play"))
        {
            Play();
        }
        if (GUILayout.Button("Pause"))
        {
            Pause();
        }
        if (GUILayout.Button("Stop"))
        {
            Stop();
        }
        EditorGUILayout.EndHorizontal();
    }

    private void ShowSpeedSilder()
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(editAnimator.length + "", GUILayout.Width(progressLableWidth));
        float sliderWidth = EditorGUIUtility.currentViewWidth - progressLableWidth - 45;
        mTotalWidth = sliderWidth - 55;
        float time = EditorGUILayout.Slider(editAnimator.currentTime, 0, editAnimator.length,
            GUILayout.Width(sliderWidth));
        EditorGUILayout.EndHorizontal();
        if (Math.Abs(time - editAnimator.currentTime) > 0.1f)
        {
            SetTime(time);
        }
    }

    private void ShowTotalProgressBar(string label)
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(label, GUILayout.Width(progressLableWidth));
        Rect rect = GUILayoutUtility.GetRect(mTotalWidth, 18);
        GUI.color = new Color(80f / 255, 40f / 255, 0f / 255, 60f / 255);
        EditorGUI.ProgressBar(rect, 1, label);
        GUI.color = Color.black;
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
    }

    private void ShowProgressBars()
    {
        int groupId = 0;
        foreach (SmartTimeline.BaseTrack baseTrack in editAnimator.allTracks)
        {
            if (baseTrack.groupIndex != groupId && baseTrack.groupIndex < editAnimator.groupIds.Length)
            {
                var centeredStyle = GUI.skin.GetStyle("Label");
                centeredStyle.alignment = TextAnchor.MiddleCenter;
                GUILayout.Label("----------   GROUP(" + editAnimator.groupIds[baseTrack.groupIndex] + ")   ----------",
                    GUILayout.ExpandWidth(true));
                centeredStyle.alignment = TextAnchor.UpperLeft;
            }
            groupId = baseTrack.groupIndex;
            if (baseTrack is SmartTimeline.ActiveTrack)
            {
                SmartTimeline.ActiveTrack activeTrack = (SmartTimeline.ActiveTrack) baseTrack;
                ShowActiveProgressBar(activeTrack);
            }
            if (baseTrack is SmartTimeline.AnimTrack)
            {
                SmartTimeline.AnimTrack animTrack = (SmartTimeline.AnimTrack) baseTrack;
                ShowAnimProgressBar(animTrack);
            }
            if (baseTrack is SmartTimeline.AudioTrack)
            {
                SmartTimeline.AudioTrack audioTrack = (SmartTimeline.AudioTrack) baseTrack;
                ShowAudioProgressBar(audioTrack);
            }
        }
    }

    private void ShowActiveProgressBar(SmartTimeline.ActiveTrack activeTrack)
    {
        if (activeTrack == null) return;
        GameObject currentSelectObj = activeTrack.currentSelectObj;
        if (currentSelectObj == null) return;
        float startPar = activeTrack.start;
        float durationPar = activeTrack.duration;
        if (editAnimator.length > 0)
        {
            startPar = activeTrack.start / editAnimator.length;
            durationPar = activeTrack.duration / editAnimator.length;
        }
        float s = startPar * mTotalWidth;
        float d = durationPar * mTotalWidth;

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(currentSelectObj.name, GUILayout.Width(progressLableWidth));
        GUILayoutUtility.GetRect(0, 18, GUILayout.Width(s));
        Rect rect = GUILayoutUtility.GetRect(0, 18, GUILayout.Width(d));
        GUI.color = new Color(229f / 255, 255f / 255, 142f / 255, 1f);
        EditorGUI.ProgressBar(rect, 1, activeTrack.active.ToString());
        GUILayout.FlexibleSpace();
        activeTrack.start = EditorGUILayout.DelayedFloatField(activeTrack.start, GUILayout.Width(50));
        EditorGUILayout.EndHorizontal();
    }

    private void ShowAnimProgressBar(SmartTimeline.AnimTrack animTrack)
    {
        if (animTrack == null) return;
        AnimationClip animationClip = animTrack.currentAnimationClip;
        if (animationClip == null) return;
        float startPar = animTrack.start;
        float durationPar = animTrack.duration;
        if (editAnimator.length > 0)
        {
            startPar = animTrack.start / editAnimator.length;
            durationPar = animTrack.duration / editAnimator.length;
        }
        float s = startPar * mTotalWidth;
        float d = durationPar * mTotalWidth;

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(animationClip.name, GUILayout.Width(progressLableWidth));
        GUILayoutUtility.GetRect(0, 18, GUILayout.Width(s));
        Rect rect = GUILayoutUtility.GetRect(0, 18, GUILayout.Width(d));
        GUI.color = new Color(92f / 255, 241f / 255, 192f / 255, 1f);
        float loopNum = animTrack.duration / animationClip.length;
        if (Math.Abs(loopNum - 1) < 0.01f)
        {
            EditorGUI.ProgressBar(rect, 1, animationClip.name);
        }
        else
        {
            EditorGUI.ProgressBar(rect, 1, animationClip.name + " (" + Math.Round(loopNum, 1) + ")");
        }
        GUILayout.FlexibleSpace();
        animTrack.start = EditorGUILayout.DelayedFloatField(animTrack.start, GUILayout.Width(50));
        EditorGUILayout.EndHorizontal();
    }

    private void ShowAudioProgressBar(SmartTimeline.AudioTrack audioTrack)
    {
        if (audioTrack == null) return;
        AudioClip aduioClip = audioTrack.audioClip;
        if (aduioClip == null) return;
        float startPar = 0;
        float durationPar = 1;
        if (editAnimator.length > 0)
        {
            startPar = audioTrack.start / editAnimator.length;
            durationPar = audioTrack.duration / editAnimator.length;
        }
        float s = startPar * mTotalWidth;
        float d = durationPar * mTotalWidth;

        Debug.Log(d);

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(aduioClip.name, GUILayout.Width(progressLableWidth));
        GUILayoutUtility.GetRect(0, 18, GUILayout.Width(s));
        Rect rect = GUILayoutUtility.GetRect(0, 18, GUILayout.Width(d));
        GUI.color = new Color(255f / 255, 150f / 255, 150f / 255, 1f);
        float loopNum = audioTrack.duration / aduioClip.length;
        if (Math.Abs(loopNum - 1) < 0.01f)
        {
            EditorGUI.ProgressBar(rect, 1, aduioClip.name);
        }
        else
        {
            EditorGUI.ProgressBar(rect, 1, aduioClip + " (" + Math.Round(loopNum, 1) + ")");
        }
        GUILayout.FlexibleSpace();
        audioTrack.start = EditorGUILayout.DelayedFloatField(audioTrack.start, GUILayout.Width(50));
        EditorGUILayout.EndHorizontal();
    }

    private void ShowTagField(SmartTimeline.BaseTrack baseTrack)
    {
        if (showTagField)
        {
            GUILayout.Label("Tag", GUILayout.Width(30));
            baseTrack.tag = EditorGUILayout.TextField(baseTrack.tag, GUILayout.Width(50));
        }
    }

    /// <summary>
    /// 刷新数据
    /// </summary>
    private void Refresh()
    {
        if (editAnimator.isPlaying) return;
        editAnimator.Refresh();
        editAnimator.allTracks.Sort((a, b) =>
        {
            if (a.groupIndex < b.groupIndex) return -1;
            if (a.groupIndex > b.groupIndex) return 1;
            if (a.groupIndex == b.groupIndex)
            {
                if (a.type < b.type) return -1;
                if (a.type > b.type) return 1;
                if (a.type == b.type)
                {
                    if (a.start < b.start) return -1;
                    if (a.start > b.start) return 1;
                    if (Math.Abs(a.start - b.start) < 0.0001f)
                    {
                        if (a.duration < b.duration) return -1;
                        if (a.duration > b.duration) return 1;
                        return 0;
                    }
                }
            }
            return 0;
        });
    }

    /// <summary>
    /// 进行预览播放
    /// </summary>
    private void Play()
    {
        if (!Application.isPlaying && Math.Abs(editAnimator.currentTime) < 0.01f)
        {
            SaveState();
        }
        editAnimator.Play();
    }

    /// <summary>
    /// 暂停预览播放
    /// </summary>
    private void Pause()
    {
        editAnimator.Pause();
    }

    /// <summary>
    /// 停止预览播放
    /// </summary>
    private void Stop()
    {
        editAnimator.Stop();
    }

    /// <summary>
    ///  跳转到
    /// </summary>
    private void SetTime(float time)
    {
        editAnimator.SetTime(time);
    }

    /// <summary>
    ///  保存GameObject状态（比如原本的显示隐藏状态等）
    /// </summary>
    private void SaveState()
    {
        editAnimator.SaveState();
    }

    /// <summary>
    ///  恢复GameObject状态
    /// </summary>
    private void ResetState()
    {
        editAnimator.ResetState();
    }
}