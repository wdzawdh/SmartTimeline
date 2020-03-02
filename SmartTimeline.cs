using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;

#endif

public class SmartTimeline : MonoBehaviour
{
    public Action playEndCallback;
    public float currentTime { get; private set; }
    public bool isPlaying { get; private set; }
    public float length { get; private set; }

    public string[] groupIds = {"ALL"};
    [HideInInspector]
    public bool recordingState;
    [HideInInspector]
    public int currentGroup;
    [HideInInspector]
    public List<ActiveTrack> activeTracks = new List<ActiveTrack>();
    [HideInInspector]
    public List<AnimTrack> animTracks = new List<AnimTrack>();
    [HideInInspector]
    public List<AudioTrack> audioTracks = new List<AudioTrack>();
    [HideInInspector]
    public List<BaseTrack> allTracks = new List<BaseTrack>();

    private float mLastTime;
    private bool mIsInfluence; //需要影响动画进度时至为true，修改完进度后再至为false

    private void PlayTrack()
    {
        for (var i = 0; i < allTracks.Count; i++)
        {
            BaseTrack baseTrack = allTracks[i];
            if (baseTrack.groupIndex != currentGroup && baseTrack.groupIndex != 0)
            {
                continue;
            }
            float s = currentTime - baseTrack.start;
            float e = currentTime - (baseTrack.start + baseTrack.duration);

            if (s >= 0 && e < 0)
            {
                if (!baseTrack.operation || mIsInfluence)
                {
                    PlayInTracks(baseTrack);
                    baseTrack.operation = true;
                    if (mIsInfluence)
                    {
                        //修改完后让下一次PlayTrack可以进来
                        baseTrack.operation = false;
                    }
                }
                PlayDurationTracks(baseTrack);
            }
            else
            {
                if (baseTrack.operation || mIsInfluence)
                {
                    PlayOutTracks(baseTrack);
                    baseTrack.operation = false;
                }
            }
        }
    }

    private void PlayInTracks(BaseTrack inTrack)
    {
        if (inTrack is AnimTrack)
        {
            AnimTrack animTrack = (AnimTrack) inTrack;
            if (animTrack.currentSelectObj != null && animTrack.currentAnimationClip != null)
            {
                if (Application.isPlaying)
                {
                    //运行状态使用Animator播放动画
                    Animator anim = animTrack.runtimeAnimator;
                    if (anim == null)
                    {
                        anim = animTrack.currentSelectObj.GetComponent<Animator>();
                        if (anim == null)
                        {
                            anim = animTrack.currentSelectObj.AddComponent<Animator>();
                        }
                        animTrack.runtimeAnimator = anim;
                    }
                    RuntimeAnimatorController orgCtrl = SetAnimOverrideController(anim, animTrack.currentAnimationClip);
                    if (orgCtrl != null)
                    {
                        //保存原AnimatorController
                        animTrack.orgAnimController = orgCtrl;
                    }
                    //还原位置
                    animTrack.currentSelectObj.transform.localPosition = animTrack.orgLocalPosition;
                    //计算播放时间点
                    float normalizedTime = (currentTime - animTrack.start) / animTrack.currentAnimationClip.length;
                    int loopIndex = (int) Mathf.Floor(normalizedTime);
                    if (normalizedTime > 1)
                    {
                        normalizedTime -= loopIndex;
                    }
                    anim.enabled = true;
                    anim.Play(animTrack.currentAnimationClip.name, 0, normalizedTime);
                    if (isPlaying)
                    {
                        anim.speed = 1;
                    }
                    else
                    {
                        anim.speed = 0;
                    }
                }
            }
        }
        if (inTrack is AudioTrack)
        {
            AudioTrack audioTrack = (AudioTrack) inTrack;
            if (audioTrack.audioClip != null)
            {
                float normalizedTime = (currentTime - audioTrack.start) / audioTrack.audioClip.length;
                int loopIndex = (int) Mathf.Floor(normalizedTime);
                if (normalizedTime > 1)
                {
                    normalizedTime -= loopIndex;
                }
                int position = (int) (normalizedTime * audioTrack.audioClip.length * 1000);
                if (loopIndex >= audioTrack.loopIndex)
                {
                    if (audioTrack.soundingAudio != null)
                    {
                        audioTrack.soundingAudio.Stop();
                        audioTrack.soundingAudio = null;
                    }
                    AudioSource audioSource = audioTrack.currentSelectObj.GetComponent<AudioSource>();
                    if (audioSource == null)
                    {
                        audioSource = audioTrack.currentSelectObj.AddComponent<AudioSource>();
                    }
                    if (isPlaying)
                    {
                        audioSource.enabled = true;
                        audioSource.clip = audioTrack.audioClip;
                        audioSource.spatialBlend = 1f;
                        audioSource.volume = 1f;
                        audioSource.time = (float) position / 1000;
                        audioSource.Play();
                        audioTrack.soundingAudio = audioSource;
                    }
                    else
                    {
                        audioSource.Pause();
                    }
                }
            }
        }
    }

    private void PlayDurationTracks(BaseTrack durationTrack)
    {
        if (durationTrack is ActiveTrack)
        {
            ActiveTrack activeTrack = (ActiveTrack) durationTrack;
            if (activeTrack.currentSelectObj != null)
            {
                activeTrack.currentSelectObj.SetActive(activeTrack.active);
            }
        }
        if (durationTrack is AnimTrack)
        {
            AnimTrack animTrack = (AnimTrack) durationTrack;
            if (animTrack.currentSelectObj != null && animTrack.currentAnimationClip != null)
            {
                float clipLength = animTrack.currentAnimationClip.length;
                float normalizedTime = (currentTime - animTrack.start) / clipLength;
                int loopIndex = (int) Mathf.Floor(normalizedTime);
                if (loopIndex > animTrack.loopIndex)
                {
                    //循环播放
                    animTrack.loopIndex = loopIndex;
                    animTrack.operation = false;
                }
                if (!Application.isPlaying)
                {
                    float time = currentTime - animTrack.start;
                    if (normalizedTime > 1)
                    {
                        time -= loopIndex * clipLength;
                    }
#if UNITY_EDITOR
                    //编辑状态使用AnimationMode采样播放动画
                    if (animTrack.currentAnimationClip != null)
                    {
                        AnimationMode.StartAnimationMode();
                        AnimationMode.SampleAnimationClip(animTrack.currentSelectObj, animTrack.currentAnimationClip,
                            time);
                    }
#endif
                }
            }
        }
        if (durationTrack is AudioTrack)
        {
            AudioTrack audioTrack = (AudioTrack) durationTrack;
            if (audioTrack.audioClip != null)
            {
                if (audioTrack.duration > audioTrack.audioClip.length && audioTrack.audioClip.length > 0)
                {
                    float normalizedTime = (currentTime - audioTrack.start) / audioTrack.audioClip.length;
                    int loopIndex = (int) Mathf.Floor(normalizedTime);
                    if (loopIndex > audioTrack.loopIndex)
                    {
                        audioTrack.loopIndex = loopIndex;
                        audioTrack.operation = false;
                    }
                }
            }
        }
    }

    private void PlayOutTracks(BaseTrack outTrack)
    {
        if (outTrack is ActiveTrack)
        {
            ActiveTrack activeTrack = outTrack as ActiveTrack;
            if (activeTrack.currentSelectObj != null)
            {
                activeTrack.currentSelectObj.SetActive(activeTrack.orgActive);
            }
        }
        if (outTrack is AnimTrack)
        {
            AnimTrack animTrack = (AnimTrack) outTrack;
            animTrack.loopIndex = 0;
        }
        if (outTrack is AudioTrack)
        {
            AudioTrack audioTrack = (AudioTrack) outTrack;
            audioTrack.loopIndex = 0;
        }
    }

    private void Awake()
    {
        currentTime = 0;
        isPlaying = false;
        Refresh();
        ResetState();
    }

    public void Update()
    {
        if (!isPlaying)
        {
            return;
        }
        if (currentTime < length)
        {
            PlayTrack();
            var now = Time.realtimeSinceStartup;
            float delTime = now - mLastTime;
            mLastTime = now;
            currentTime += delTime;
        }
        else
        {
            Stop();
        }
    }

    public void Refresh()
    {
        if (isPlaying)
        {
            return;
        }
        allTracks.Clear();
        allTracks.AddRange(activeTracks);
        allTracks.AddRange(animTracks);
        allTracks.AddRange(audioTracks);
        float end = 0;
        foreach (var baseTrack in allTracks)
        {
            float trackEnd = baseTrack.start + baseTrack.duration;
            if (trackEnd > end)
            {
                end = trackEnd;
            }
        }
        length = end;
    }

    public void SaveState()
    {
        if (!recordingState) return;
        for (var i = 0; i < activeTracks.Count; i++)
        {
            ActiveTrack activeTrack = activeTracks[i];
            if (activeTrack == null) break;
            GameObject currentSelectObj = activeTrack.currentSelectObj;
            if (currentSelectObj != null)
            {
                activeTrack.orgActive = currentSelectObj.activeSelf;
            }
        }
        for (int i = 0; i < animTracks.Count; i++)
        {
            AnimTrack animTrack = animTracks[i];
            if (animTrack == null) break;
            animTrack.orgLocalPosition = animTrack.currentSelectObj.transform.localPosition;
        }
        recordingState = false;
    }

    public void Pause()
    {
        isPlaying = false;
        mIsInfluence = true;
        PlayTrack();
        mIsInfluence = false;
    }

    public void Play()
    {
        if (isPlaying) return;
        isPlaying = true;
        mLastTime = Time.realtimeSinceStartup;
        PlayTrack();
    }

    public void Step(float delTime)
    {
        currentTime += delTime;
        if (currentTime >= length)
        {
            currentTime = 0;
        }
        isPlaying = false;
        mIsInfluence = true;
        PlayTrack();
        mIsInfluence = false;
    }

    public void Stop()
    {
        currentTime = 0;
        isPlaying = false;
        mIsInfluence = true;
        PlayTrack();
        mIsInfluence = false;
        ResetState();
        playEndCallback?.Invoke();
        if (!Application.isPlaying)
        {
#if UNITY_EDITOR
            AnimationMode.StopAnimationMode();
#endif
        }
    }

    public void SetTime(float time)
    {
        currentTime = time;
        mIsInfluence = true;
        PlayTrack();
        mIsInfluence = false;
    }

    public void SetState(int state)
    {
        if (state < groupIds.Length)
        {
            currentGroup = state;
        }
    }

    public float GetTime()
    {
        return currentTime;
    }

    public float GetDuration()
    {
        return length;
    }

    public void ResetState()
    {
        //从后向前还原
        List<BaseTrack> baseTracks = new List<BaseTrack>(allTracks);
        baseTracks.Sort((a, b) =>
        {
            if (a.start > b.start) return -1;
            if (a.start < b.start) return 1;
            return 0;
        });
        for (var i = 0; i < baseTracks.Count; i++)
        {
            BaseTrack baseTrack = baseTracks[i];
            if (baseTrack is ActiveTrack)
            {
                ActiveTrack activeTrack = (ActiveTrack) baseTrack;
                if (activeTrack.currentSelectObj != null)
                {
                    activeTrack.currentSelectObj.SetActive(activeTrack.orgActive);
                }
            }
            if (baseTrack is AnimTrack)
            {
                AnimTrack animTrack = (AnimTrack) baseTrack;
                animTrack.loopIndex = 0;
                if (animTrack.currentSelectObj != null)
                {
                    animTrack.currentSelectObj.transform.localPosition = animTrack.orgLocalPosition;
                }
                if (Application.isPlaying)
                {
                    if (animTrack.runtimeAnimator != null)
                    {
                        ResetAnimOverrideController(animTrack.runtimeAnimator, animTrack.orgAnimController);
                        animTrack.runtimeAnimator.Rebind();
                        animTrack.runtimeAnimator.speed = 1;
                    }
                }
                else
                {
#if UNITY_EDITOR
                    if (animTrack.currentAnimationClip != null)
                    {
                        AnimationMode.StartAnimationMode();
                        AnimationMode.SampleAnimationClip(animTrack.currentSelectObj,
                            animTrack.currentAnimationClip, 0);
                    }
#endif
                }
            }
            if (baseTrack is AudioTrack)
            {
                AudioTrack audioTrack = (AudioTrack) baseTrack;
                audioTrack.loopIndex = 0;
                if (audioTrack.soundingAudio != null)
                {
                    audioTrack.soundingAudio.Stop();
                    audioTrack.soundingAudio = null;
                }
            }
            baseTrack.operation = false;
        }
    }

    public T GetTrackByTag<T>(string _tag) where T : BaseTrack
    {
        if (_tag == null) return null;
        foreach (BaseTrack baseTrack in allTracks)
        {
            if (baseTrack is T)
            {
                T track = (T) baseTrack;
                if (_tag.Equals(track.tag))
                {
                    return track;
                }
            }
        }
        return null;
    }

    public static void ReplaceActive(ActiveTrack orgTrack, GameObject replaceGameObject, bool active)
    {
        if (orgTrack == null || replaceGameObject == null) return;
        orgTrack.currentSelectObj = replaceGameObject;
        orgTrack.active = active;
    }

    public static void ReplaceAnimClip(AnimTrack orgTrack, GameObject replaceGameObject, AnimationClip clip)
    {
        if (orgTrack == null || clip == null || replaceGameObject == null) return;
        orgTrack.currentSelectObj = replaceGameObject;
        orgTrack.orgLocalPosition = replaceGameObject.transform.localPosition;
        orgTrack.runtimeAnimator = null;
        orgTrack.currentAnimationClip = clip;
        orgTrack.duration = clip.length;
    }

    private RuntimeAnimatorController SetAnimOverrideController(Animator animator, AnimationClip animationClip)
    {
        foreach (var parameter in animator.parameters)
        {
            string parameterName = parameter.name;
            switch (parameter.type)
            {
                case AnimatorControllerParameterType.Int:
                    animator.SetInteger(parameterName, -1);
                    break;
                case AnimatorControllerParameterType.Float:
                    animator.SetFloat(parameterName, -1);
                    break;
                case AnimatorControllerParameterType.Bool:
                    animator.SetBool(parameterName, false);
                    break;
                case AnimatorControllerParameterType.Trigger:
                    animator.ResetTrigger(parameterName);
                    break;
            }
        }
        RuntimeAnimatorController myController = animator.runtimeAnimatorController;
        AnimatorOverrideController myOverrideController = myController as AnimatorOverrideController;
        RuntimeAnimatorController controller;
        if (myOverrideController != null)
        {
            controller = myOverrideController.runtimeAnimatorController;
        }
        else
        {
            controller = myController;
        }
        AnimatorOverrideController animatorOverride = new AnimatorOverrideController();
        animatorOverride.runtimeAnimatorController = controller;
        animatorOverride[animationClip.name] = animationClip;
        animator.runtimeAnimatorController = animatorOverride;
        return myOverrideController == null ? myController : null;
    }

    private void ResetAnimOverrideController(Animator animator, RuntimeAnimatorController orgController)
    {
        if (animator == null || orgController == null)
        {
            return;
        }
        RuntimeAnimatorController myController = animator.runtimeAnimatorController;
        if (myController is AnimatorOverrideController)
        {
            animator.runtimeAnimatorController = orgController;
        }
    }

    [Serializable]
    public class ActiveTrack : BaseTrack
    {
        public GameObject currentSelectObj;
        public bool active = true;
        public bool orgActive = true;

        protected override int getType()
        {
            return 0;
        }
    }

    [Serializable]
    public class AnimTrack : BaseTrack
    {
        public GameObject currentSelectObj;
        public Vector3 orgLocalPosition;
        public AnimationClip currentAnimationClip;
        [NonSerialized]
        public RuntimeAnimatorController orgAnimController;
        [NonSerialized]
        public Animator runtimeAnimator;
        [NonSerialized]
        public int loopIndex;

        protected override int getType()
        {
            return 1;
        }
    }

    [Serializable]
    public class AudioTrack : BaseTrack
    {
        public GameObject currentSelectObj;
        public AudioClip audioClip;
        [NonSerialized]
        public AudioSource soundingAudio;
        [NonSerialized]
        public int loopIndex;

        protected override int getType()
        {
            return 2;
        }
    }

    [Serializable]
    public abstract class BaseTrack
    {
        public int type;
        public int groupIndex;
        public float start;
        public float duration;
        public string tag;
        [NonSerialized]
        public bool operation;

        protected BaseTrack()
        {
            type = getType();
        }

        protected abstract int getType();
    }
}