using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PolarAI.Scripts.AICore.ElevenLabs
{
    public class ElevenLabsExample : MonoBehaviour
    {
        [Header("ElevenLabs API 设置")] public string ElevenLabsApiKey = "";
        public string VoiceId = "";

        [Header("UI 组件")] public TMP_InputField TextInput;
        public Button GenBtn;
        public Button PlayStopBtn;
        public Slider VoiceSlider;
        public AudioSource AudioSource;

        [Header("可选：状态文本")] public TMP_Text StatusText;
        public TMP_Text TimeText; // 显示当前时间/总时间

        private ElevenLabsCore ElevenLabsCore = new ElevenLabsCore();
        private AudioClip _currentClip;
        private bool _isGenerating = false;
        private bool _isPlaying = false;
        private bool _isDraggingSlider = false;

        private void Start()
        {
            // 初始化 ElevenLabsCore
            ElevenLabsCore.Initialize(ElevenLabsApiKey, VoiceId);

            // 设置 UI 事件监听
            SetupUIEvents();

            // 初始化 UI 状态
            UpdateButtonStates();
            UpdateStatusText("就绪");

            // 初始化进度条
            if (VoiceSlider != null)
            {
                VoiceSlider.minValue = 0f;
                VoiceSlider.maxValue = 1f;
                VoiceSlider.value = 0f;
                VoiceSlider.interactable = false;
            }
        }

        private void SetupUIEvents()
        {
            // 生成按钮
            if (GenBtn != null)
            {
                GenBtn.onClick.AddListener(OnGenerateButtonClicked);
            }

            // 播放/停止按钮
            if (PlayStopBtn != null)
            {
                PlayStopBtn.onClick.AddListener(OnPlayStopButtonClicked);
            }

            // 进度滑块 - 支持拖拽调整播放位置
            if (VoiceSlider != null)
            {
                VoiceSlider.onValueChanged.AddListener(OnProgressChanged);

                // 检测拖拽开始和结束
                var sliderEvents = VoiceSlider.gameObject.AddComponent<SliderDragHandler>();
                sliderEvents.onBeginDrag = () => { _isDraggingSlider = true; };
                sliderEvents.onEndDrag = () =>
                {
                    _isDraggingSlider = false;
                    if (AudioSource != null && _currentClip != null)
                    {
                        // 根据滑块位置设置播放时间
                        AudioSource.time = VoiceSlider.value * _currentClip.length;
                    }
                };
            }
        }

        private void OnGenerateButtonClicked()
        {
            if (_isGenerating)
            {
                UpdateStatusText("正在生成中，请稍候...");
                return;
            }

            if (TextInput == null || string.IsNullOrWhiteSpace(TextInput.text))
            {
                UpdateStatusText("请输入要转换的文本");
                return;
            }

            if (string.IsNullOrWhiteSpace(ElevenLabsApiKey))
            {
                UpdateStatusText("错误：请设置 ElevenLabs API Key");
                Debug.LogError("ElevenLabs API Key 未设置！");
                return;
            }

            if (string.IsNullOrWhiteSpace(VoiceId))
            {
                UpdateStatusText("错误：请设置 Voice ID");
                Debug.LogError("Voice ID 未设置！");
                return;
            }

            GenerateSpeech(TextInput.text);
        }

        private void GenerateSpeech(string text)
        {
            _isGenerating = true;
            UpdateButtonStates();
            UpdateStatusText("正在生成语音...");

            // 停止当前播放
            if (_isPlaying && AudioSource != null)
            {
                AudioSource.Stop();
                _isPlaying = false;
            }

            // 重置进度条
            if (VoiceSlider != null)
            {
                VoiceSlider.value = 0f;
                VoiceSlider.interactable = false;
            }

            UpdateTimeText(0f, 0f);

            // 调用 ElevenLabsCore 生成语音
            ElevenLabsCore.TextToSound(text, OnSpeechGenerated);
        }

        private void OnSpeechGenerated(AudioClip clip, bool success)
        {
            _isGenerating = false;

            if (success && clip != null)
            {
                _currentClip = clip;
                UpdateStatusText("语音生成成功！");
                Debug.Log("语音生成成功，时长: " + clip.length + " 秒");

                // 启用进度条
                if (VoiceSlider != null)
                {
                    VoiceSlider.interactable = true;
                }

                // 自动播放
                PlayAudio();
            }
            else
            {
                UpdateStatusText("语音生成失败");
                Debug.LogError("语音生成失败");
            }

            UpdateButtonStates();
        }

        private void OnPlayStopButtonClicked()
        {
            if (_currentClip == null)
            {
                UpdateStatusText("请先生成语音");
                return;
            }

            if (_isPlaying)
            {
                StopAudio();
            }
            else
            {
                PlayAudio();
            }
        }

        private void PlayAudio()
        {
            if (AudioSource == null)
            {
                Debug.LogError("AudioSource 未设置！");
                return;
            }

            if (_currentClip == null)
            {
                UpdateStatusText("没有可播放的音频");
                return;
            }

            AudioSource.clip = _currentClip;
            AudioSource.Play();
            _isPlaying = true;
            UpdateStatusText("正在播放...");
            UpdateButtonStates();
        }

        private void StopAudio()
        {
            if (AudioSource != null)
            {
                AudioSource.Stop();
            }

            _isPlaying = false;
            UpdateStatusText("已停止播放");
            UpdateButtonStates();
        }

        private void OnProgressChanged(float value)
        {
            // 如果用户正在拖拽滑块，更新时间显示
            if (_isDraggingSlider && _currentClip != null)
            {
                float currentTime = value * _currentClip.length;
                UpdateTimeText(currentTime, _currentClip.length);
            }
        }

        private void Update()
        {
            // 更新播放进度
            if (_isPlaying && AudioSource != null && AudioSource.isPlaying && _currentClip != null)
            {
                // 如果用户没有拖拽滑块，更新进度条
                if (!_isDraggingSlider && VoiceSlider != null)
                {
                    float progress = AudioSource.time / _currentClip.length;
                    VoiceSlider.value = progress;
                }

                // 更新时间显示
                UpdateTimeText(AudioSource.time, _currentClip.length);
            }

            // 检测音频播放结束
            if (_isPlaying && AudioSource != null && !AudioSource.isPlaying)
            {
                _isPlaying = false;
                UpdateStatusText("播放完成");
                UpdateButtonStates();

                // 重置进度条到开头
                if (VoiceSlider != null)
                {
                    VoiceSlider.value = 0f;
                }

                if (_currentClip != null)
                {
                    UpdateTimeText(0f, _currentClip.length);
                }
            }
        }

        private void UpdateButtonStates()
        {
            // 更新生成按钮状态
            if (GenBtn != null)
            {
                GenBtn.interactable = !_isGenerating;
                var btnText = GenBtn.GetComponentInChildren<TMP_Text>();
                if (btnText != null)
                {
                    btnText.text = _isGenerating ? "生成中..." : "生成语音";
                }
            }

            // 更新播放/停止按钮状态
            if (PlayStopBtn != null)
            {
                PlayStopBtn.interactable = _currentClip != null && !_isGenerating;
                var btnText = PlayStopBtn.GetComponentInChildren<TMP_Text>();
                if (btnText != null)
                {
                    btnText.text = _isPlaying ? "停止" : "播放";
                }
            }
        }

        private void UpdateStatusText(string message)
        {
            if (StatusText != null)
            {
                StatusText.text = message;
            }

            Debug.Log($"[ElevenLabs] {message}");
        }

        private void UpdateTimeText(float currentTime, float totalTime)
        {
            if (TimeText != null)
            {
                TimeText.text = $"{FormatTime(currentTime)} / {FormatTime(totalTime)}";
            }
        }

        private string FormatTime(float seconds)
        {
            int minutes = Mathf.FloorToInt(seconds / 60f);
            int secs = Mathf.FloorToInt(seconds % 60f);
            return $"{minutes:00}:{secs:00}";
        }

        private void OnDestroy()
        {
            // 清理事件监听
            if (GenBtn != null)
            {
                GenBtn.onClick.RemoveListener(OnGenerateButtonClicked);
            }

            if (PlayStopBtn != null)
            {
                PlayStopBtn.onClick.RemoveListener(OnPlayStopButtonClicked);
            }

            if (VoiceSlider != null)
            {
                VoiceSlider.onValueChanged.RemoveListener(OnProgressChanged);
            }

            // 停止播放
            if (AudioSource != null && AudioSource.isPlaying)
            {
                AudioSource.Stop();
            }
        }

        // 公共方法：允许外部调用生成语音
        public void GenerateSpeechFromText(string text)
        {
            if (!string.IsNullOrWhiteSpace(text))
            {
                GenerateSpeech(text);
            }
        }

        // 公共方法：设置 API Key
        public void SetApiKey(string apiKey)
        {
            ElevenLabsApiKey = apiKey;
            ElevenLabsCore.Initialize(ElevenLabsApiKey, VoiceId);
        }

        // 公共方法：设置 Voice ID
        public void SetVoiceId(string voiceId)
        {
            VoiceId = voiceId;
            ElevenLabsCore.Initialize(ElevenLabsApiKey, VoiceId);
        }
    }

    // 辅助类：检测滑块拖拽事件
    public class SliderDragHandler : MonoBehaviour, UnityEngine.EventSystems.IBeginDragHandler,
        UnityEngine.EventSystems.IEndDragHandler
    {
        public System.Action onBeginDrag;
        public System.Action onEndDrag;

        public void OnBeginDrag(UnityEngine.EventSystems.PointerEventData eventData)
        {
            onBeginDrag?.Invoke();
        }

        public void OnEndDrag(UnityEngine.EventSystems.PointerEventData eventData)
        {
            onEndDrag?.Invoke();
        }
    }
}