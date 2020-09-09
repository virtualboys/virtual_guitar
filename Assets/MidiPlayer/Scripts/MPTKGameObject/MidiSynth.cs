//#define DEBUGPERF
//#define DEBUGOnAudioFilterRead
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using UnityEngine;
using UnityEngine.Events;
using MEC;

namespace MidiPlayerTK
{
    public enum fluid_loop
    {
        FLUID_UNLOOPED = 0,
        FLUID_LOOP_DURING_RELEASE = 1,
        FLUID_NOTUSED = 2,
        FLUID_LOOP_UNTIL_RELEASE = 3
    }

    public enum fluid_synth_status
    {
        FLUID_SYNTH_CLEAN,
        FLUID_SYNTH_PLAYING,
        FLUID_SYNTH_QUIET,
        FLUID_SYNTH_STOPPED
    }

    // Flags to choose the interpolation method 
    public enum fluid_interp
    {
        // no interpolation: Fastest, but questionable audio quality
        FLUID_INTERP_NONE = 0,
        // Straight-line interpolation: A bit slower, reasonable audio quality
        FLUID_INTERP_LINEAR = 1,
        // Fourth-order interpolation: Requires 50 % of the whole DSP processing time, good quality 
        FLUID_INTERP_DEFAULT = 4, // Not yet implemented
        FLUID_INTERP_4THORDER = 4,
        FLUID_INTERP_7THORDER = 7, // Not yet implemented
        FLUID_INTERP_HIGHEST = 7
    }

    /// <summary>
    /// </summary>
    /// Base class for Midi Synthesizer. Migrated from fluidsynth.
    /// It's not recommended to instanciate this class. Instead use MidiFilePlayer or MidiStreamPlayer.
    public class MidiSynth : MonoBehaviour
    {
        //! @cond NODOC
        public VoiceAudioSource AudiosourceTemplate;
        public AudioSource CoreAudioSource;
        //! @endcond

        /// <summary>
        /// If true then Midi events are read and play from a dedicated thread. If false, MidiSynth will use AudioSource gameobject to play sound.
        /// This properties must be defined before running the application from the inspector. The default is true. 
        /// </summary>
        public bool MPTK_CorePlayer;

        /// <summary>
        /// Set or Get sample rate output of the synth. -1:default, 0:24000, 1:36000, 2:48000, 3:60000, 4:72000, 5:84000, 6:96000. 
        /// It's better to stop playing before changing on fly to avoid bad noise.
        /// </summary>
        public int MPTK_IndexSynthRate
        {
            get { return indexSynthRate; }
            set
            {
                indexSynthRate = value;
                if (VerboseSynth) Debug.Log("MPTK_ChangeSynthRate " + indexSynthRate);
                if (indexSynthRate < 0)
                {
                    // No change
                    OnAudioConfigurationChanged(false);
                }
                else
                {
                    if (indexSynthRate > 6) indexSynthRate = 6;
                    if (CoreAudioSource != null) CoreAudioSource.Stop();
                    AudioConfiguration ac = AudioSettings.GetConfiguration();
                    ac.sampleRate = 24000 + (MPTK_IndexSynthRate * 12000);
                    AudioSettings.Reset(ac);
                    if (ActiveVoices != null)
                        for (int i = 0; i < ActiveVoices.Count; i++)
                            ActiveVoices[i].output_rate = OutputRate;
                    if (FreeVoices != null)
                        for (int i = 0; i < FreeVoices.Count; i++)
                            FreeVoices[i].output_rate = OutputRate;
                    if (CoreAudioSource != null) CoreAudioSource.Play();
                }
            }
        }
        [SerializeField]
        [HideInInspector]
        private int indexSynthRate = -1;

        private int[] tabDspBufferSize = new int[] { 64, 128, 256, 512, 1024, 2048 };

        /// <summary>
        /// Set or Get sample rate output of the synth. -1:default, 0:24000, 1:36000, 2:48000, 3:60000, 4:72000, 5:84000, 6:96000. 
        /// It's better to stop playing before changing on fly to avoid bad noise.
        /// </summary>
        public int MPTK_IndexSynthBuffSize
        {
            get { return indexBuffSize; }
            set
            {
                indexBuffSize = value;
                if (VerboseSynth) Debug.Log("MPTK_IndexSynthBuffSize " + indexBuffSize);
                if (indexBuffSize < 0)
                {
                    // No change
                    OnAudioConfigurationChanged(false);
                }
                else
                {
                    if (indexBuffSize > 5) indexBuffSize = 5;
                    if (CoreAudioSource != null) CoreAudioSource.Stop();
                    AudioConfiguration ac = AudioSettings.GetConfiguration();
                    ac.dspBufferSize = tabDspBufferSize[indexBuffSize];
                    AudioSettings.Reset(ac);
                    //if (ActiveVoices != null)
                    //    for (int i = 0; i < ActiveVoices.Count; i++)
                    //        ActiveVoices[i].output_rate = OutputRate;
                    //if (FreeVoices != null)
                    //    for (int i = 0; i < FreeVoices.Count; i++)
                    //        FreeVoices[i].output_rate = OutputRate;
                    if (CoreAudioSource != null) CoreAudioSource.Play();
                }
            }
        }
        [SerializeField]
        [HideInInspector]
        private int indexBuffSize = -1;


        /// <summary>
        /// If true (default) then Midi events are sent automatically to the midi player.
        /// Set to false if you want to process events without playing sound. 
        /// OnEventNotesMidi Unity Event can be used to process each notes.
        /// </summary>
        public bool MPTK_DirectSendToPlayer;

        /// <summary>
        /// Should accept change tempo from Midi Events ? 
        /// </summary>
        public bool MPTK_EnableChangeTempo;

        /// <summary>
        /// MaxDistance to use for PauseOnDistance
        /// </summary>
        public float MPTK_MaxDistance
        {
            get
            {
                return maxDistance;
            }
            set
            {
                try
                {
                    maxDistance = value;
                    SetMaxDistanceAudioSource();
                }
                catch (System.Exception ex)
                {
                    MidiPlayerGlobal.ErrorDetail(ex);
                }
            }
        }

        //! @cond NODOC

        protected void SetMaxDistanceAudioSource()
        {
            //Debug.Log("Set Max Distance " + maxDistance);
            if (MPTK_CorePlayer)
            {
                CoreAudioSource.maxDistance = maxDistance;
            }
            else
            {
                AudiosourceTemplate.Audiosource.maxDistance = maxDistance;
                if (ActiveVoices != null)
                    for (int i = 0; i < ActiveVoices.Count; i++)
                    {
                        fluid_voice voice = ActiveVoices[i];
                        if (voice.VoiceAudio != null)
                            voice.VoiceAudio.Audiosource.maxDistance = maxDistance;
                    }
                if (FreeVoices != null)
                    for (int i = 0; i < FreeVoices.Count; i++)
                    {
                        fluid_voice voice = FreeVoices[i];
                        if (voice.VoiceAudio != null)
                            voice.VoiceAudio.Audiosource.maxDistance = maxDistance;
                    }
            }
        }
        //! @endcond

        [SerializeField]
        [HideInInspector]
        private float maxDistance;

        /// <summary>
        /// Should the Midi playing must be paused if distance between AudioListener and MidiFilePlayer is greater than MaxDistance
        /// </summary>
        public bool MPTK_PauseOnDistance;

        /// <summary>
        /// Should change pan from Midi Events or from SoundFont ? 
        /// </summary>
        public bool MPTK_EnablePanChange;

        /// <summary>
        /// Volume of midi playing. 
        /// Must be >=0 and <= 1
        /// </summary>
        public float MPTK_Volume
        {
            get { return volume; }
            set
            {

                if (value >= 0f && value <= 1f) volume = value; else Debug.LogWarning("MidiFilePlayer - Set Volume value not valid : " + value);
            }
        }

        [SerializeField]
        [HideInInspector]
        private float volume = 0.5f;

        /// <summary>
        /// Transpose note from -24 to 24
        /// </summary>
        public int MPTK_Transpose
        {
            get { return transpose; }
            set { if (value >= -24 && value <= 24f) transpose = value; else Debug.LogWarning("MidiFilePlayer - Set Transpose value not valid : " + value); }
        }

        /// <summary>
        /// Should accept change Preset for Drum canal 10 ? 
        /// Disabled by default. Could sometimes create bad sound with midi files not really compliant with the Midi norm.
        /// </summary>
        public bool MPTK_EnablePresetDrum;

        /// <summary>
        /// Log for each wave to be played
        /// </summary>
        public bool MPTK_LogWave;

        /// <summary>
        /// Count of the active voices (playing) - Readonly
        /// </summary>
        public int MPTK_StatVoiceCountActive;

        /// <summary>
        /// Count of the free voices for reusing on need. Older than AutoCleanVoiceTime are removed when count is over than AutoCleanVoiceLimit - Readonly
        /// </summary>
        public int MPTK_StatVoiceCountFree;

        /// <summary>
        /// Percentage of voice reused during the synth life. 0: any reuse, 100:all voice reused (unattainable, of course!)
        /// </summary>
        public float MPTK_StatVoiceRatioReused;

        /// <summary>
        /// Count of voice played since the start of the synth
        /// </summary>
        public int MPTK_StatVoicePlayed;

        /// <summary>
        /// Free voices older than MPTK_AutoCleanVoiceLimit are removed when count is over than MPTK_AutoCleanVoiceTime
        /// </summary>
        [Tooltip("Auto Clean Voice Greater Than")]
        public int MPTK_AutoCleanVoiceLimit;

        [Tooltip("Auto Clean Voice Older Than (millisecond)")]
        public float MPTK_AutoCleanVoiceTime;

        [Tooltip("Preset are often composed with 2 or more samples. Classically for left and right channel. Check this to play only the first sample found")]
        public bool MPTK_PlayOnlyFirstWave;
        public bool MPTK_ApplyFilter;
        public bool MPTK_ApplyReverb;
        public bool MPTK_ApplyChorus;
        public bool MPTK_ApplyRealTimeModulator;
        public bool MPTK_ApplyModLfo;
        public bool MPTK_ApplyVibLfo;

        //! @cond NODOC
        /// <summary>
        /// Time from synth init (ms)
        /// </summary>
        [Tooltip("Time when synth has been started")]
        public double TimeAtInit;

        protected MidiLoad miditoplay;

        /// <summary>
        /// Distance to the listener. Calculated only if MPTK_PauseOnDistance = true
        /// </summary>
        [HideInInspector]
        public float distanceToListener;

        [SerializeField]
        [HideInInspector]
        public int transpose = 0;

        public fluid_channel[] Channels;          /** the channels */
        private List<fluid_voice> ActiveVoices;              /** the synthesis processes */

        private List<fluid_voice> FreeVoices;              /** the synthesis processes */
        //public ConcurrentQueue<MPTKEvent> QueueEvents;
        protected Queue<SynthCommand> QueueSynthCommand;
        protected Queue<List<MPTKEvent>> QueueMidiEvents;

        public class SynthCommand
        {
            public enum enCmd { StartEvent, StopEvent, ClearAllVoices, NoteOffAll }
            public enCmd Command;
            public MPTKEvent MidiEvent;
        }

        /* fluid_settings_old_t settings_old;  the old synthesizer settings */
        //TBC fluid_settings_t* settings;         /** the synthesizer settings */

        //int polyphony;                     /** maximum polyphony */
        [HideInInspector]
        public int FLUID_BUFSIZE = 64; // was a const

        private int dspBufferSize;

        public fluid_interp InterpolationMethode;

        [Range(0.01f, 0.3f)]
        [Tooltip("Sample is stopped when amplitude is below this value")]
        // replace amplitude_that_reaches_noise_floor 
        public float CutOffVolume = 0.1f;
        public float gain = 1f;

        //public const uint DRUM_INST_MASK = 0x80000000;

        public bool VerboseSynth;
        public bool VerboseVoice;
        public bool VerboseGenerator;
        public bool VerboseCalcGen;
        public bool VerboseController;
        public bool VerboseEnvVolume;
        public bool VerboseEnvModulation;
        public bool VerboseFilter;
        public bool VerboseVolume;

        [HideInInspector]
        public float OutputRate;                /** The sample rate */

        //public int midi_channels = 16;                 /** the number of MIDI channels (>= 16) */
        //int audio_channels;                /** the number of audio channels (1 channel=left+right) */

        // the number of (stereo) 'sub'groups from the synth. Typically equal to audio_channels.
        // Only one used with Unity
        // int audio_groups;                 

        //int effects_channels = 2;              /** the number of effects channels (= 2) */

        fluid_synth_status state;                /** the synthesizer state */
        //uint ticks;                /** the number of audio samples since the start */

        //the start in msec, as returned by system clock 
        //uint start;

        // How many audio buffers are used? (depends on nr of audio channels / groups)
        // Only one buffer used with Unity
        //int nbuf;     

        [Header("Attributes below are only for No Core Audio Mode")]
        [Tooltip("Only for no Core Audio")]
        public bool AdsrSimplified;

        /// <summary>
        /// Should play on a weak device (cheaper smartphone) ? Apply only with AudioSource mode (MPTK_CorePlayer=False)
        /// Playing Midi files with WeakDevice activated could cause some bad interpretation of Midi Event, consequently bad sound.
        /// </summary>
        [Tooltip("Apply only with AudioSource mode (MPTK_CorePlayer=False)")]
        public bool MPTK_WeakDevice;

        /// <summary>
        /// [Only when CorePlayer=False] Define a minimum release time at noteoff in 100 iem nanoseconds. Default 50 ms is a good tradeoff. Below some unpleasant sound could be heard. Useless when MPTK_CorePlayer is true.
        /// </summary>
        [Range(0, 5000000)]
        [Tooltip("Only for no Core Audio")]
        public uint MPTK_ReleaseTimeMin = 500000;

        [Tooltip("Only for no Core Audio")]
        [Range(0.00f, 5.0f)]
        public float LfoAmpFreq = 1f;

        [Tooltip("Only for no Core Audio")]
        [Range(0.01f, 5.0f)]
        public float LfoVibFreq = 1f;

        [Tooltip("Only for no Core Audio")]
        [Range(0.01f, 5.0f)]
        public float LfoVibAmp = 1f;

        [Tooltip("Only for no Core Audio")]
        [Range(0.01f, 5.0f)]
        public float LfoToFilterMod = 1f;

        [Tooltip("Only for no Core Audio")]
        [Range(0.01f, 5.0f)]
        public float FilterEnvelopeMod = 1f;

        [Tooltip("Only for no Core Audio")]
        [Range(-2000f, 3000f)]
        public float FilterOffset = 1000f;

        [Tooltip("Only for no Core Audio")]
        [Range(0.01f, 5.0f)]
        public float FilterQMod = 1f;

        [Tooltip("Only for no Core Audio")]
        [Range(0f, 1f)]
        public float ReverbMix = 0f;

        [Tooltip("Only for no Core Audio")]
        [Range(0f, 1f)]
        public float ChorusMix = 0f;

        [Range(0, 100)]
        [Tooltip("Smooth Volume Change (for no core audio)")]
        public int DampVolume = 0;


        //! @endcond

        [Header("Events associated to the synth")]


        /// <summary>
        /// Unity event fired at awake of the synthesizer. Name of the gameobject component is passed as a parameter.
        ///! @code
        /// ...
        /// if (!midiStreamPlayer.OnEventSynthAwake.HasEvent())
        ///    midiStreamPlayer.OnEventSynthAwake.AddListener(StartLoadingSynth);
        /// ...
        /// public void StartLoadingSynth(string name)
        /// {
        ///     Debug.LogFormat("Synth {0} loading", name);
        /// }
        ///! @endcode
        /// </summary>
        public EventSynthClass OnEventSynthAwake;

        /// <summary>
        /// Unity event fired at start of the synthesizer. Name of the gameobject component is passed as a parameter.
        ///! @code
        /// ...
        /// if (!midiStreamPlayer.OnEventStartSynth.HasEvent())
        ///    midiStreamPlayer.OnEventStartSynth.AddListener(EndLoadingSynth);
        /// ...
        /// public void EndLoadingSynth(string name)
        /// {
        ///    Debug.LogFormat("Synth {0} loaded", name);
        ///    midiStreamPlayer.MPTK_PlayEvent(
        ///       new MPTKEvent() { Command = MPTKCommand.PatchChange, Value = CurrentPatchInstrument, Channel = StreamChannel});
        /// }
        ///! @endcode
        /// </summary>
        public EventSynthClass OnEventSynthStarted;

        private float[] left_buf;
        private float[] right_buf;
        //float[,] fx_left_buf;
        //float[,] fx_right_buf;

        //int cur;                           /** the current sample in the audio buffers to be output */
        //int dither_index;		/* current index in random dither value buffer: fluid_synth_(write_s16|dither_s16) */


        //fluid_tuning_t[][] tuning;           /** 128 banks of 128 programs for the tunings */
        //fluid_tuning_t cur_tuning;         /** current tuning in the iteration */

        // The midi router. Could be done nicer.
        //Indicates, whether the audio thread is currently running.Note: This simple scheme does -not- provide 100 % protection against thread problems, for example from MIDI thread and shell thread
        //fluid_mutex_t busy;
        //fluid_midi_router_t* midi_router;


        // has the synth module been initialized? 
        private static int fluid_synth_initialized = 0;

        //default modulators SF2.01 page 52 ff:
        //There is a set of predefined default modulators. They have to be explicitly overridden by the sound font in order to turn them off.

        private static HiMod default_vel2att_mod = new HiMod();        /* SF2.01 section 8.4.1  */
        private static HiMod default_vel2filter_mod = new HiMod();     /* SF2.01 section 8.4.2  */
        private static HiMod default_at2viblfo_mod = new HiMod();      /* SF2.01 section 8.4.3  */
        private static HiMod default_mod2viblfo_mod = new HiMod();     /* SF2.01 section 8.4.4  */
        private static HiMod default_att_mod = new HiMod();            /* SF2.01 section 8.4.5  */
        private static HiMod default_pan_mod = new HiMod();            /* SF2.01 section 8.4.6  */
        private static HiMod default_expr_mod = new HiMod();           /* SF2.01 section 8.4.7  */
        private static HiMod default_reverb_mod = new HiMod();         /* SF2.01 section 8.4.8  */
        private static HiMod default_chorus_mod = new HiMod();         /* SF2.01 section 8.4.9  */
        private static HiMod default_pitch_bend_mod = new HiMod();     /* SF2.01 section 8.4.10 */

        private int countvoiceReused;

        /* reverb presets */
        //        static fluid_revmodel_presets_t revmodel_preset[] = {
        //	/* name */    /* roomsize */ /* damp */ /* width */ /* level */
        //	{ "Test 1",          0.2f,      0.0f,       0.5f,       0.9f },
        //    { "Test 2",          0.4f,      0.2f,       0.5f,       0.8f },
        //    { "Test 3",          0.6f,      0.4f,       0.5f,       0.7f },
        //    { "Test 4",          0.8f,      0.7f,       0.5f,       0.6f },
        //    { "Test 5",          0.8f,      1.0f,       0.5f,       0.5f },
        //    { NULL, 0.0f, 0.0f, 0.0f, 0.0f }
        //};

        /// <summary>
        /// From fluid_sys.c - fluid_utime() returns the time in micro seconds. this time should only be used to measure duration(relative times). 
        /// </summary>
        /// <param name=""></param>
        /// <returns></returns>
        //double fluid_utime()
        //{
        //    //fprintf(stderr, "fluid_cpu_frequency:%f fluid_utime:%f\n", fluid_cpu_frequency, rdtsc() / fluid_cpu_frequency);

        //    //return (rdtsc() / fluid_cpu_frequency);
        //    return AudioSettings.dspTime;
        //}

        /// <summary>
        /// returns the current time in milliseconds. This time should only be used in relative time measurements.
        /// </summary>
        /// <returns></returns>
        //int fluid_curtime()
        //{
        //    // replace GetTickCount() :Retrieves the number of milliseconds that have elapsed since the system was started, up to 49.7 days.
        //    return System.Environment.TickCount;
        //}

        public void Awake()
        {
            if (VerboseSynth) Debug.Log("Awake MidiSynth");
            try
            {
                OnEventSynthAwake.Invoke(this.name);
                MidiPlayerGlobal.InitPath();
            }
            catch (System.Exception ex)
            {
                MidiPlayerGlobal.ErrorDetail(ex);
            }
        }


        public void Start()
        {
            try
            {
                if (VerboseSynth) Debug.Log("Start MidiSynth");

                if (AudiosourceTemplate == null)
                {
                    if (VerboseSynth)
                        Debug.LogWarningFormat("AudiosourceTemplate not defined in the {0} inspector, search one", this.name);
                    AudiosourceTemplate = FindObjectOfType<VoiceAudioSource>();
                    if (AudiosourceTemplate == null)
                    {
                        Debug.LogErrorFormat("No VoiceAudioSource template found for the audiosource synth {0}", this.name);
                    }
                }

                if (CoreAudioSource == null)
                {
                    if (VerboseSynth)
                        Debug.LogWarningFormat("CoreAudioSource not defined in the {0} inspector, search one", this.name);
                    CoreAudioSource = GetComponent<AudioSource>();
                    if (CoreAudioSource == null)
                    {
                        Debug.LogErrorFormat("No AudioSource template found for the core synth {0}", this.name);
                    }
                }

                AudioSettings.OnAudioConfigurationChanged += OnAudioConfigurationChanged;

                MPTK_IndexSynthRate = indexSynthRate;

                //AudioConfiguration GetConfiguration = AudioSettings.GetConfiguration();
                //polyphony = GetConfiguration.numRealVoices;
                //OutputRate = GetConfiguration.sampleRate;
                //midi_channels = 16;
                //audio_channels = 2;
                //dspBufferSize = GetConfiguration.dspBufferSize;
                //FLUID_BUFSIZE = dspBufferSize;

                fluid_dsp_float.fluid_dsp_float_config();

                if (VerboseSynth)
                    InfoAudio();

                /* The number of buffers is determined by the higher number of nr
                 * groups / nr audio channels.  If LADSPA is unused, they should be
                 * the same. */
                //nbuf = audio_channels;
                //if (audio_groups > nbuf)
                //{
                //    nbuf = audio_groups;
                //}

                /* as soon as the synth is created it starts playing. */
                // Too soon state = fluid_synth_status.FLUID_SYNTH_PLAYING;

                /* allocate all channel objects */
                ////////channels = new fluid_channel_t[midi_channels];
                ////////for (int i = 0; i < channels.Length; i++)
                ////////    channels[i] = new fluid_channel_t(this, i);

                ////////voices = new List<fluid_voice_t>();

                ///* Allocate the sample buffers */
                //left_buf = NULL;
                //right_buf = NULL;
                //fx_left_buf = NULL;
                //fx_right_buf = NULL;

                ///* Left and right audio buffers */
                //left_buf =  FLUID_ARRAY(fluid_real_t *, nbuf);
                //right_buf = FLUID_ARRAY(fluid_real_t *, nbuf);

                //if ((left_buf == NULL) || (right_buf == NULL))
                //{
                //    FLUID_LOG(FLUID_ERR, "Out of memory");
                //    goto error_recovery;
                //}

                //FLUID_MEMSET(left_buf, 0, nbuf * sizeof(fluid_real_t*));
                //FLUID_MEMSET(right_buf, 0, nbuf * sizeof(fluid_real_t*));

                //for (i = 0; i < nbuf; i++)
                //{

                left_buf = new float[FLUID_BUFSIZE];
                right_buf = new float[FLUID_BUFSIZE];

                //    if ((left_buf[i] == NULL) || (right_buf[i] == NULL))
                //    {
                //        FLUID_LOG(FLUID_ERR, "Out of memory");
                //        goto error_recovery;
                //    }
                //}

                ///* Effects audio buffers */

                //fx_left_buf = FLUID_ARRAY(fluid_real_t *, effects_channels);
                //fx_right_buf = FLUID_ARRAY(fluid_real_t *, effects_channels);

                //if ((fx_left_buf == NULL) || (fx_right_buf == NULL))
                //{
                //    FLUID_LOG(FLUID_ERR, "Out of memory");
                //    goto error_recovery;
                //}

                //FLUID_MEMSET(fx_left_buf, 0, 2 * sizeof(fluid_real_t*));
                //FLUID_MEMSET(fx_right_buf, 0, 2 * sizeof(fluid_real_t*));

                //for (i = 0; i < effects_channels; i++)
                //{

                //////fx_left_buf = new float[effects_channels, FLUID_BUFSIZE];
                //////fx_right_buf = new float[effects_channels, FLUID_BUFSIZE];

                //    if ((fx_left_buf[i] == NULL) || (fx_right_buf[i] == NULL))
                //    {
                //        FLUID_LOG(FLUID_ERR, "Out of memory");
                //        goto error_recovery;
                //    }
                //}

                ///* allocate the reverb module */
                //reverb = new_fluid_revmodel();
                //if (reverb == NULL)
                //{
                //    FLUID_LOG(FLUID_ERR, "Out of memory");
                //    goto error_recovery;
                //}

                //fluid_synth_set_reverb(synth,
                //    FLUID_REVERB_DEFAULT_ROOMSIZE,
                //    FLUID_REVERB_DEFAULT_DAMP,
                //    FLUID_REVERB_DEFAULT_WIDTH,
                //    FLUID_REVERB_DEFAULT_LEVEL);

                ///* allocate the chorus module */
                //chorus = new_fluid_chorus(sample_rate);
                //if (chorus == NULL)
                //{
                //    FLUID_LOG(FLUID_ERR, "Out of memory");
                //    goto error_recovery;
                //}

                //cur = FLUID_BUFSIZE;
                //dither_index = 0;

                /* FIXME */
                //start = (uint)(DateTime.Now.Ticks / fluid_voice.Nano100ToMilli); // milliseconds:  fluid_curtime();

                OnEventSynthStarted.Invoke(this.name);
            }
            catch (System.Exception ex)
            {
                MidiPlayerGlobal.ErrorDetail(ex);
            }
        }

        void OnAudioConfigurationChanged(bool deviceWasChanged)
        {
            AudioConfiguration GetConfiguration = AudioSettings.GetConfiguration();
            OutputRate = GetConfiguration.sampleRate;
            dspBufferSize = GetConfiguration.dspBufferSize;
            if (VerboseSynth)
            {
                Debug.Log("OnAudioConfigurationChanged - " + (deviceWasChanged ? "Device was changed" : "Reset was called"));
                Debug.Log("   dspBufferSize:" + dspBufferSize);
                Debug.Log("   OutputRate:" + OutputRate);
            }
        }

        private void InfoAudio()
        {
            int bufferLenght;
            int numBuffers;
            // Two methods
            AudioSettings.GetDSPBufferSize(out bufferLenght, out numBuffers);
            AudioConfiguration ac = AudioSettings.GetConfiguration();
            Debug.Log("------InfoAudio------");
            Debug.Log("  " + (MPTK_CorePlayer ? "Core Player Activated" : "AudioSource Player Activated"));
            Debug.Log("  bufferLenght:" + bufferLenght + " 2nd method: " + ac.dspBufferSize);
            Debug.Log("  numBuffers:" + numBuffers);
            Debug.Log("  outputSampleRate:" + AudioSettings.outputSampleRate + " 2nd method: " + ac.sampleRate);
            Debug.Log("  speakerMode:" + AudioSettings.speakerMode);
#if DEBUGOnAudioFilterRead
            Debug.Log("  deltaTime DSP:" + deltaTimeDSP + " Ticks:" + deltaTimeTicks);
#endif
            Debug.Log("---------------------");
        }

        /// <summary>
        /// Init the synthetizer. Prefabs automatically initialize the synthetizer (see events). It's not usefull to call this method.
        /// </summary>
        /// <param name="channelCount">Number of channel to create, default 16. Any other values are experimental!</param>
        public void MPTK_InitSynth(int channelCount = 16)
        {
            fluid_synth_init(channelCount);
        }

        /// <summary>
        /// Clear all sound
        /// </summary>
        /// <param name="destroyAudioSource">Destroy also audioSource (default:false)</param>
        ///! @code
        ///  if (GUILayout.Button("Clear"))
        ///     midiStreamPlayer.MPTK_ClearAllSound(true);
        ///! @endcode
        public void MPTK_ClearAllSound(bool destroyAudioSource = false)
        {
            Timing.RunCoroutine(ThreadClearAllSound(true));
        }

        public IEnumerator<float> ThreadClearAllSound(bool destroyAudioSource = false)
        {
#if DEBUGNOTE
            numberNote = -1;
#endif
            //Debug.Log("ThreadClearAllSound");
            yield return Timing.WaitUntilDone(Timing.RunCoroutine(ThreadReleaseAll()), false);

            if (!MPTK_CorePlayer && destroyAudioSource)
            {
                yield return Timing.WaitUntilDone(Timing.RunCoroutine(ThreadWaitAllStop()), false);
                yield return Timing.WaitUntilDone(Timing.RunCoroutine(ThreadDestroyAllVoice()), false);
            }
            yield return 0;
        }

        /// <summary>
        /// Cut the sound gradually
        /// </summary>
        /// <returns></returns>
        private IEnumerator<float> ThreadReleaseAll()
        {
            if (MPTK_CorePlayer)
            {
                if (QueueSynthCommand != null)
                    QueueSynthCommand.Enqueue(new SynthCommand() { Command = SynthCommand.enCmd.NoteOffAll });
            }
            else
            {
                for (int i = 0; i < ActiveVoices.Count; i++)
                {
                    fluid_voice voice = ActiveVoices[i];
                    if (voice != null && (voice.status == fluid_voice_status.FLUID_VOICE_ON || voice.status == fluid_voice_status.FLUID_VOICE_SUSTAINED))
                    {
                        //Debug.LogFormat("ReleaseAll {0} / {1}", voice.IdVoice, ActiveVoices.Count);
                        yield return Timing.WaitUntilDone(Timing.RunCoroutine(voice.Release()));
                    }
                }
            }
        }

        /// <summary>
        /// Wait all audio source not playing with time out of 2 seconds
        /// </summary>
        /// <returns></returns>
        private IEnumerator<float> ThreadWaitAllStop()
        {
            //Debug.Log("ThreadWaitAllStop");
            int countplaying = 999999;
            DateTime timeout = DateTime.Now + TimeSpan.FromSeconds(2);
            while (countplaying > 0 && timeout > DateTime.Now)
            {
                countplaying = 0;
                for (int i = 0; i < ActiveVoices.Count; i++)
                {
                    fluid_voice voice = ActiveVoices[i];
                    if (voice != null && (voice.status == fluid_voice_status.FLUID_VOICE_ON || voice.status == fluid_voice_status.FLUID_VOICE_SUSTAINED))
                    {
                        countplaying++;
                    }
                }
                //Debug.LogFormat("   delay: {0} sec. countplaying: {1}", (timeout - DateTime.Now).TotalSeconds, countplaying);
            }
            //Debug.Log("ThreadWaitAllStop end - countplaying:" + countplaying);

            yield return 0;
        }

        //! @cond NODOC
        /// Remove AudioSource not playing
        /// </summary>
        protected IEnumerator<float> ThreadDestroyAllVoice()
        {
            //Debug.Log("ThreadDestroyAllVoice");
            try
            {
                //VoiceAudioSource[] voicesList = GetComponentsInChildren<VoiceAudioSource>();
                //Debug.LogFormat("DestroyAllVoice {0}", (voicesList != null ? voicesList.Length.ToString() : "no voice found"));
                //if (voicesList != null)
                //{
                //    foreach (VoiceAudioSource voice in voicesList)
                //        try
                //        {
                //            //Debug.Log("Destroy " + voice.IdVoice + " " + (voice.Audiosource.clip != null ? voice.Audiosource.clip.name : "no clip"));
                //            //Don't delete audio source template
                //            if (voice.name.StartsWith("VoiceAudioId_"))
                //                Destroy(voice.gameObject);
                //        }
                //        catch (System.Exception ex)
                //        {
                //            MidiPlayerGlobal.ErrorDetail(ex);
                //        }
                //    Voices.Clear();
                //}
                if (ActiveVoices != null)
                {
                    if (MPTK_CorePlayer)
                        QueueSynthCommand.Enqueue(new SynthCommand() { Command = SynthCommand.enCmd.ClearAllVoices });
                    else
                    {
                        for (int i = 0; i < ActiveVoices.Count; i++)
                        {
                            try
                            {
                                fluid_voice voice = ActiveVoices[i];
                                if (voice != null && voice.VoiceAudio != null)
                                {
                                    Debug.Log("Destroy " + voice.IdVoice + " " + (voice.VoiceAudio.Audiosource.clip != null ? voice.VoiceAudio.Audiosource.clip.name : "no clip"));
                                    //Don't delete audio source template
                                    if (voice.VoiceAudio.name.StartsWith("VoiceAudioId_"))
                                        Destroy(voice.VoiceAudio.gameObject);
                                }
                            }
                            catch (System.Exception ex)
                            {
                                MidiPlayerGlobal.ErrorDetail(ex);
                            }
                        }
                        ActiveVoices.Clear();
                    }
                }
            }
            catch (System.Exception ex)
            {
                MidiPlayerGlobal.ErrorDetail(ex);
            }
            yield return 0;
        }

        // Does all the initialization for this module.
        public void fluid_synth_init(int channelCount)
        {
            fluid_synth_initialized++;
            MPTK_ResetStat();

            TimeAtInit = Time.realtimeSinceStartup * 1000d;
            fluid_voice.LastId = 0;

#if TRAP_ON_FPE
            /* Turn on floating point exception traps */
            feenableexcept(FE_DIVBYZERO | FE_UNDERFLOW | FE_OVERFLOW | FE_INVALID);
#endif
            Channels = new fluid_channel[channelCount];
            for (int i = 0; i < Channels.Length; i++)
                Channels[i] = new fluid_channel(this, i);

            if (VerboseSynth) Debug.LogFormat("fluid_synth_init. Init: {0}, Channels: {1}", fluid_synth_initialized, Channels.Length);

            ActiveVoices = new List<fluid_voice>();
            FreeVoices = new List<fluid_voice>();
            QueueSynthCommand = new Queue<SynthCommand>();
            QueueMidiEvents = new Queue<List<MPTKEvent>>();

            fluid_conv.fluid_conversion_config();

            //TBC fluid_dsp_float_config();
            //fluid_sys_config();
            //init_dither(); // pour fluid_synth_write_s16 ?

            /* SF2.01 page 53 section 8.4.1: MIDI Note-On Velocity to Initial Attenuation */
            fluid_mod_set_source1(default_vel2att_mod, /* The modulator we are programming here */
                (int)fluid_mod_src.FLUID_MOD_VELOCITY,    /* Source. VELOCITY corresponds to 'index=2'. */
                (int)fluid_mod_flags.FLUID_MOD_GC           /* Not a MIDI continuous controller */
                | (int)fluid_mod_flags.FLUID_MOD_CONCAVE    /* Curve shape. Corresponds to 'type=1' */
                | (int)fluid_mod_flags.FLUID_MOD_UNIPOLAR   /* Polarity. Corresponds to 'P=0' */
                | (int)fluid_mod_flags.FLUID_MOD_NEGATIVE   /* Direction. Corresponds to 'D=1' */
            );
            fluid_mod_set_source2(default_vel2att_mod, 0, 0); /* No 2nd source */
            fluid_mod_set_dest(default_vel2att_mod, (int)fluid_gen_type.GEN_ATTENUATION);  /* Target: Initial attenuation */
            fluid_mod_set_amount(default_vel2att_mod, 960.0f);          /* Modulation amount: 960 */

            /* SF2.01 page 53 section 8.4.2: MIDI Note-On Velocity to Filter Cutoff
             * Have to make a design decision here. The specs don't make any sense this way or another.
             * One sound font, 'Kingston Piano', which has been praised for its quality, tries to
             * override this modulator with an amount of 0 and positive polarity (instead of what
             * the specs say, D=1) for the secondary source.
             * So if we change the polarity to 'positive', one of the best free sound fonts works...
             */
            fluid_mod_set_source1(default_vel2filter_mod, (int)fluid_mod_src.FLUID_MOD_VELOCITY, /* Index=2 */
                (int)fluid_mod_flags.FLUID_MOD_GC                        /* CC=0 */
                | (int)fluid_mod_flags.FLUID_MOD_LINEAR                  /* type=0 */
                | (int)fluid_mod_flags.FLUID_MOD_UNIPOLAR                /* P=0 */
                | (int)fluid_mod_flags.FLUID_MOD_NEGATIVE                /* D=1 */
            );
            fluid_mod_set_source2(default_vel2filter_mod, (int)fluid_mod_src.FLUID_MOD_VELOCITY, /* Index=2 */
                (int)fluid_mod_flags.FLUID_MOD_GC                                 /* CC=0 */
                | (int)fluid_mod_flags.FLUID_MOD_SWITCH                           /* type=3 */
                | (int)fluid_mod_flags.FLUID_MOD_UNIPOLAR                         /* P=0 */
                                                                                  // do not remove       | FLUID_MOD_NEGATIVE                         /* D=1 */
                | (int)fluid_mod_flags.FLUID_MOD_POSITIVE                         /* D=0 */
            );
            fluid_mod_set_dest(default_vel2filter_mod, (int)fluid_gen_type.GEN_FILTERFC);        /* Target: Initial filter cutoff */
            fluid_mod_set_amount(default_vel2filter_mod, -2400);

            /* SF2.01 page 53 section 8.4.3: MIDI Channel pressure to Vibrato LFO pitch depth */
            fluid_mod_set_source1(default_at2viblfo_mod, (int)fluid_mod_src.FLUID_MOD_CHANNELPRESSURE, /* Index=13 */
                (int)fluid_mod_flags.FLUID_MOD_GC                        /* CC=0 */
                | (int)fluid_mod_flags.FLUID_MOD_LINEAR                  /* type=0 */
                | (int)fluid_mod_flags.FLUID_MOD_UNIPOLAR                /* P=0 */
                | (int)fluid_mod_flags.FLUID_MOD_POSITIVE                /* D=0 */
            );
            fluid_mod_set_source2(default_at2viblfo_mod, 0, 0); /* no second source */
            fluid_mod_set_dest(default_at2viblfo_mod, (int)fluid_gen_type.GEN_VIBLFOTOPITCH);        /* Target: Vib. LFO => pitch */
            fluid_mod_set_amount(default_at2viblfo_mod, 50);

            /* SF2.01 page 53 section 8.4.4: Mod wheel (Controller 1) to Vibrato LFO pitch depth */
            fluid_mod_set_source1(default_mod2viblfo_mod, 1, /* Index=1 */
                (int)fluid_mod_flags.FLUID_MOD_CC                        /* CC=1 */
                | (int)fluid_mod_flags.FLUID_MOD_LINEAR                  /* type=0 */
                | (int)fluid_mod_flags.FLUID_MOD_UNIPOLAR                /* P=0 */
                | (int)fluid_mod_flags.FLUID_MOD_POSITIVE                /* D=0 */
            );
            fluid_mod_set_source2(default_mod2viblfo_mod, 0, 0); /* no second source */
            fluid_mod_set_dest(default_mod2viblfo_mod, (int)fluid_gen_type.GEN_VIBLFOTOPITCH);        /* Target: Vib. LFO => pitch */
            fluid_mod_set_amount(default_mod2viblfo_mod, 50);

            /* SF2.01 page 55 section 8.4.5: MIDI continuous controller 7 to initial attenuation*/
            fluid_mod_set_source1(default_att_mod, 7,                     /* index=7 */
                (int)fluid_mod_flags.FLUID_MOD_CC                              /* CC=1 */
                | (int)fluid_mod_flags.FLUID_MOD_CONCAVE                       /* type=1 */
                | (int)fluid_mod_flags.FLUID_MOD_UNIPOLAR                      /* P=0 */
                | (int)fluid_mod_flags.FLUID_MOD_NEGATIVE                      /* D=1 */
            );
            fluid_mod_set_source2(default_att_mod, 0, 0);                 /* No second source */
            fluid_mod_set_dest(default_att_mod, (int)fluid_gen_type.GEN_ATTENUATION);         /* Target: Initial attenuation */
            fluid_mod_set_amount(default_att_mod, 960.0f);                 /* Amount: 960 */

            /* SF2.01 page 55 section 8.4.6 MIDI continuous controller 10 to Pan Position */
            fluid_mod_set_source1(default_pan_mod, 10,                    /* index=10 */
                (int)fluid_mod_flags.FLUID_MOD_CC                              /* CC=1 */
                | (int)fluid_mod_flags.FLUID_MOD_LINEAR                        /* type=0 */
                | (int)fluid_mod_flags.FLUID_MOD_BIPOLAR                       /* P=1 */
                | (int)fluid_mod_flags.FLUID_MOD_POSITIVE                      /* D=0 */
            );
            fluid_mod_set_source2(default_pan_mod, 0, 0);                 /* No second source */
            fluid_mod_set_dest(default_pan_mod, (int)fluid_gen_type.GEN_PAN);

            // Target: pan - Amount: 500. The SF specs $8.4.6, p. 55 syas: "Amount = 1000 tenths of a percent". 
            // The center value (64) corresponds to 50%, so it follows that amount = 50% x 1000/% = 500. 
            fluid_mod_set_amount(default_pan_mod, 500.0f);


            /* SF2.01 page 55 section 8.4.7: MIDI continuous controller 11 to initial attenuation*/
            fluid_mod_set_source1(default_expr_mod, 11,                     /* index=11 */
                (int)fluid_mod_flags.FLUID_MOD_CC                              /* CC=1 */
                | (int)fluid_mod_flags.FLUID_MOD_CONCAVE                       /* type=1 */
                | (int)fluid_mod_flags.FLUID_MOD_UNIPOLAR                      /* P=0 */
                | (int)fluid_mod_flags.FLUID_MOD_NEGATIVE                      /* D=1 */
            );
            fluid_mod_set_source2(default_expr_mod, 0, 0);                 /* No second source */
            fluid_mod_set_dest(default_expr_mod, (int)fluid_gen_type.GEN_ATTENUATION);         /* Target: Initial attenuation */
            fluid_mod_set_amount(default_expr_mod, 960.0f);                 /* Amount: 960 */



            /* SF2.01 page 55 section 8.4.8: MIDI continuous controller 91 to Reverb send */
            fluid_mod_set_source1(default_reverb_mod, 91,                 /* index=91 */
                (int)fluid_mod_flags.FLUID_MOD_CC                              /* CC=1 */
                | (int)fluid_mod_flags.FLUID_MOD_LINEAR                        /* type=0 */
                | (int)fluid_mod_flags.FLUID_MOD_UNIPOLAR                      /* P=0 */
                | (int)fluid_mod_flags.FLUID_MOD_POSITIVE                      /* D=0 */
            );
            fluid_mod_set_source2(default_reverb_mod, 0, 0);              /* No second source */
            fluid_mod_set_dest(default_reverb_mod, (int)fluid_gen_type.GEN_REVERBSEND);       /* Target: Reverb send */
            fluid_mod_set_amount(default_reverb_mod, 200);                /* Amount: 200 ('tenths of a percent') */

            /* SF2.01 page 55 section 8.4.9: MIDI continuous controller 93 to Reverb send */
            fluid_mod_set_source1(default_chorus_mod, 93,                 /* index=93 */
                (int)fluid_mod_flags.FLUID_MOD_CC                              /* CC=1 */
                | (int)fluid_mod_flags.FLUID_MOD_LINEAR                        /* type=0 */
                | (int)fluid_mod_flags.FLUID_MOD_UNIPOLAR                      /* P=0 */
                | (int)fluid_mod_flags.FLUID_MOD_POSITIVE                      /* D=0 */
            );
            fluid_mod_set_source2(default_chorus_mod, 0, 0);              /* No second source */
            fluid_mod_set_dest(default_chorus_mod, (int)fluid_gen_type.GEN_CHORUSSEND);       /* Target: Chorus */
            fluid_mod_set_amount(default_chorus_mod, 200);                /* Amount: 200 ('tenths of a percent') */



            /* SF2.01 page 57 section 8.4.10 MIDI Pitch Wheel to Initial Pitch ... */
            fluid_mod_set_source1(default_pitch_bend_mod, (int)fluid_mod_src.FLUID_MOD_PITCHWHEEL, /* Index=14 */
                (int)fluid_mod_flags.FLUID_MOD_GC                              /* CC =0 */
                | (int)fluid_mod_flags.FLUID_MOD_LINEAR                        /* type=0 */
                | (int)fluid_mod_flags.FLUID_MOD_BIPOLAR                       /* P=1 */
                | (int)fluid_mod_flags.FLUID_MOD_POSITIVE                      /* D=0 */
            );
            fluid_mod_set_source2(default_pitch_bend_mod, (int)fluid_mod_src.FLUID_MOD_PITCHWHEELSENS,  /* Index = 16 */
                (int)fluid_mod_flags.FLUID_MOD_GC                                        /* CC=0 */
                | (int)fluid_mod_flags.FLUID_MOD_LINEAR                                  /* type=0 */
                | (int)fluid_mod_flags.FLUID_MOD_UNIPOLAR                                /* P=0 */
                | (int)fluid_mod_flags.FLUID_MOD_POSITIVE                                /* D=0 */
            );
            fluid_mod_set_dest(default_pitch_bend_mod, (int)fluid_gen_type.GEN_PITCH);                 /* Destination: Initial pitch */
            fluid_mod_set_amount(default_pitch_bend_mod, 12700.0f);                 /* Amount: 12700 cents */

            /* as soon as the synth is created it starts playing. */
            state = fluid_synth_status.FLUID_SYNTH_PLAYING;

        }

        /// <summary>
        /// Reset voices statistics 
        /// </summary>
        public void MPTK_ResetStat()
        {
            MPTK_StatVoicePlayed = 0;
            countvoiceReused = 0;
            MPTK_StatVoiceRatioReused = 0;
        }

        /*
         * fluid_mod_set_source1
         */
        void fluid_mod_set_source1(HiMod mod, int src, int flags)
        {
            mod.Src1 = (byte)src;
            mod.Flags1 = (byte)flags;
        }

        /*
         * fluid_mod_set_source2
         */
        void fluid_mod_set_source2(HiMod mod, int src, int flags)
        {
            mod.Src2 = (byte)src;
            mod.Flags2 = (byte)flags;
        }

        /*
         * fluid_mod_set_dest
         */
        void fluid_mod_set_dest(HiMod mod, int dest)
        {
            mod.Dest = (byte)dest;
        }

        /*
         * fluid_mod_set_amount
         */
        void fluid_mod_set_amount(HiMod mod, float amount)
        {
            mod.Amount = amount;
        }

        /// <summary>
        /// Enable or disable a channel. Must be applied after each call to MPTK_Play or MPTK_RePlay (which reset the channel state).
        /// </summary>
        /// <param name="channel">must be between 0 and 15</param>
        /// <param name="enable">true to enable</param>
        public void MPTK_ChannelEnableSet(int channel, bool enable)
        {
            if (CheckParamChannel(channel))
                Channels[channel].enabled = enable;
        }

        /// <summary>
        /// Get channel state.
        /// </summary>
        /// <param name="channel">must be between 0 and 15</param>
        public bool MPTK_ChannelEnableGet(int channel)
        {
            if (CheckParamChannel(channel))
                return Channels[channel].enabled;
            else
                return false;
        }

        /// <summary>
        /// Get channel preset indx.
        /// </summary>
        /// <param name="channel">must be between 0 and 15</param>
        public int MPTK_ChannelPresetGetIndex(int channel)
        {
            if (CheckParamChannel(channel))
                return Channels[channel].prognum;
            else
                return -1;
        }

        /// <summary>
        /// Get channel bank.
        /// </summary>
        /// <param name="channel">must be between 0 and 15</param>
        public int MPTK_ChannelBankGetIndex(int channel)
        {
            if (CheckParamChannel(channel))
                return Channels[channel].banknum;
            else
                return -1;
        }

        /// <summary>
        /// Get channel current preset name.
        /// </summary>
        /// <param name="channel">must be between 0 and 15</param>
        public string MPTK_ChannelPresetGetName(int channel)
        {
            if (Channels[channel].preset != null && CheckParamChannel(channel))
                return Channels[channel].preset.Name;
            else
                return null;
        }

        /// <summary>
        /// Get channel count. The norm is 16.
        /// </summary>
        /// <param name="channel">must be between 0 and 15</param>
        public int MPTK_ChannelCount()
        {
            if (CheckParamChannel(0))
                return Channels.Length;
            return 0;
        }

        private bool CheckParamChannel(int channel)
        {
            if (channel < 0 || channel >= Channels.Length)
            {
                Debug.LogWarningFormat("MPTK_ChannelEnable: channels are not created");
                return false;
            }
            if (channel < 0 || channel >= Channels.Length)
            {
                Debug.LogWarningFormat("MPTK_ChannelEnable: incorrect value for channel {0}", channel);
                return false;
            }
            if (Channels[channel] == null)
            {
                Debug.LogWarningFormat("MPTK_ChannelEnable: channel {0} is not defined", channel);
                return false;
            }
            return true;
        }

        /// <summary>
        /// Change the preset and bank for the channel. 
        /// When playing a Midi file, the preset is set by channel with the Midi message Patch Change. 
        /// The bank is changed with a ControlChange Midi message.  
        /// The new value of the bank is local for the channel, the preset list is not updated.
        /// To change globally the bank, use instead the golbal methods: MidiPlayerGlobal.MPTK_SelectBankInstrument or MidiPlayerGlobal.MPTK_SelectBankDrum
        /// </summary>
        /// <param name="channel">There is 16 channels available in the Midi norm.</param>
        /// <param name="preset">The count of presets is dependant of the soundfont selected</param>
        /// <param name="newbank">optionnal, use the default bank defined globally</param>
        /// <returns>true if preset change is done</returns>
        public bool MPTK_ChannelPresetChange(int channel, int preset, int newbank = -1)
        {
            if (CheckParamChannel(channel))
            {
                // Take the default bank for this channel or a new bank
                int bank = newbank < 0 ? Channels[channel].banknum : newbank;

                ImSoundFont sfont = MidiPlayerGlobal.ImSFCurrent;
                if (sfont == null)
                    Debug.LogWarningFormat("MPTK_ChannelPresetChange: no soundfont defined");
                else if (bank < 0 || bank >= sfont.Banks.Length)
                    Debug.LogWarningFormat("MPTK_ChannelPresetChange: bank {0} is outside the limits [{1} - {2}] for sfont {3}", bank, 0, sfont.Banks.Length, sfont.SoundFontName);
                else if (sfont.Banks[bank] == null || sfont.Banks[bank].defpresets == null)
                    Debug.LogWarningFormat("MPTK_ChannelPresetChange: bank {0} is not defined with sfont {1}", bank, sfont.SoundFontName);
                else if (preset < 0 || preset >= sfont.Banks[bank].defpresets.Length)
                    Debug.LogWarningFormat("MPTK_ChannelPresetChange: preset {0} is outside the limits [{1} - {2}] for sfont {3}", preset, 0, sfont.Banks[bank].defpresets.Length, sfont.SoundFontName);
                else if (sfont.Banks[bank].defpresets[preset] == null)
                    Debug.LogWarningFormat("MPTK_ChannelPresetChange: preset {0} is not defined with sfont {1}", preset, sfont.SoundFontName);
                else
                {
                    Channels[channel].banknum = bank;
                    fluid_synth_program_change(channel, preset);
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Allocate a synthesis voice. This function is called by a
        /// soundfont's preset in response to a noteon event.
        /// The returned voice comes with default modulators installed(velocity-to-attenuation, velocity to filter, ...)
        /// Note: A single noteon event may create any number of voices, when the preset is layered. Typically 1 (mono) or 2 (stereo).
        /// </summary>
        /// <param name="synth"></param>
        /// <param name="hiSample"></param>
        /// <param name="chan"></param>
        /// <param name="key"></param>
        /// <param name="vel"></param>
        /// <returns></returns>
        public fluid_voice fluid_synth_alloc_voice(HiSample hiSample, int chan, int key, int vel)
        {
            fluid_voice voice = null;
            fluid_channel channel = null;
            MPTK_StatVoicePlayed++;

            /*   fluid_mutex_lock(synth.busy); /\* Don't interfere with the audio thread *\/ */
            /*   fluid_mutex_unlock(synth.busy); */

            // check if there's an available voice with same wave
            for (int indexVoice = 0; indexVoice < FreeVoices.Count;)
            {
                fluid_voice v = FreeVoices[indexVoice];
                if (v.sample.Name == hiSample.Name)
                {
                    voice = v;
                    FreeVoices.RemoveAt(indexVoice);
                    countvoiceReused++;
                    if (VerboseVoice) Debug.LogFormat("Reuse voice {0} {1}", v.IdVoice, v.sample.Name);
                    break;
                }
                indexVoice++;
            }

#if DEBUGPERF
            DebugPerf("After find existing voice:");
#endif
            // No found existing voice, instanciate a new one
            if (voice == null)
            {
                if (VerboseVoice) Debug.LogFormat("Create voice for {0} {1} hz", hiSample.Name, hiSample.SampleRate);

                voice = new fluid_voice(this);

                if (MPTK_CorePlayer)
                {
                    voice.VoiceAudio = null;
                    // All soundfont wave are mono, channels is normally equals to 1
                    voice.sample = DicAudioWave.GetWave(hiSample.Name);

                    if (voice.sample == null)
                    {
                        Debug.LogWarningFormat("fluid_synth_alloc_voice - Clip {0} data not loaded", hiSample.Name);
                        return null;
                    }
                    //else Debug.LogFormat("fluid_synth_alloc_voice - load wave from dict. {0} Length:{1} SynthSampleRate:{2}", hiSample.Name, voice.sample.Data.Length, sample_rate);

                }
                else
                {
                    AudioClip clip = DicAudioClip.Get(hiSample.Name);
                    if (clip == null || clip.loadState != AudioDataLoadState.Loaded)
                    {
                        // if (VerboseVoice)
                        Debug.LogWarningFormat("fluid_synth_alloc_voice - Clip {0} not ready to play or not found", hiSample.Name);
                        return null;
                    }
                    voice.sample = hiSample;
                    voice.VoiceAudio = Instantiate<VoiceAudioSource>(AudiosourceTemplate);
                    voice.VoiceAudio.fluidvoice = voice;
                    voice.VoiceAudio.synth = this;
                    voice.VoiceAudio.transform.position = AudiosourceTemplate.transform.position;
                    voice.VoiceAudio.transform.SetParent(AudiosourceTemplate.transform.parent);
                    voice.VoiceAudio.name = "VoiceAudioId_" + voice.IdVoice;
                    voice.VoiceAudio.Audiosource.clip = clip;
                    // seems to have no effect, issue open with Unity
                    voice.VoiceAudio.hideFlags = VerboseVoice ? HideFlags.None : HideFlags.HideInHierarchy;
                }

#if DEBUGPERF
                DebugPerf("After instanciate voice:");
#endif
            }

            // Apply change on each voice
            if (MPTK_CorePlayer)
            {
                // Done with ThreadCorePlay
            }
            else
            {
                if (voice.VoiceAudio != null)
                    voice.VoiceAudio.Audiosource.spatialBlend = MPTK_PauseOnDistance ? 1f : 0f;
                MoveVoiceToFree();
                AutoCleanVoice();
            }

            if (chan < 0 || chan >= Channels.Length)
            {
                Debug.LogFormat("Channel out of range chan:{0}", chan);
                chan = 0;
            }
            channel = Channels[chan];

            // Defined default voice value even when reuse of a voice
            voice.fluid_voice_init(channel, key, vel/*, gain*/);

#if DEBUGPERF
            DebugPerf("After fluid_voice_init:");
#endif
            /* add the default modulators to the synthesis process. */
            voice.mods = new List<HiMod>();
            voice.fluid_voice_add_mod(MidiSynth.default_vel2att_mod, fluid_voice_addorover_mod.FLUID_VOICE_DEFAULT);    /* SF2.01 $8.4.1  */
            voice.fluid_voice_add_mod(MidiSynth.default_vel2filter_mod, fluid_voice_addorover_mod.FLUID_VOICE_DEFAULT); /* SF2.01 $8.4.2  */
            voice.fluid_voice_add_mod(MidiSynth.default_at2viblfo_mod, fluid_voice_addorover_mod.FLUID_VOICE_DEFAULT);  /* SF2.01 $8.4.3  */
            voice.fluid_voice_add_mod(MidiSynth.default_mod2viblfo_mod, fluid_voice_addorover_mod.FLUID_VOICE_DEFAULT); /* SF2.01 $8.4.4  */
            voice.fluid_voice_add_mod(MidiSynth.default_att_mod, fluid_voice_addorover_mod.FLUID_VOICE_DEFAULT);        /* SF2.01 $8.4.5  */
            voice.fluid_voice_add_mod(MidiSynth.default_pan_mod, fluid_voice_addorover_mod.FLUID_VOICE_DEFAULT);        /* SF2.01 $8.4.6  */
            voice.fluid_voice_add_mod(MidiSynth.default_expr_mod, fluid_voice_addorover_mod.FLUID_VOICE_DEFAULT);       /* SF2.01 $8.4.7  */
            voice.fluid_voice_add_mod(MidiSynth.default_reverb_mod, fluid_voice_addorover_mod.FLUID_VOICE_DEFAULT);     /* SF2.01 $8.4.8  */
            voice.fluid_voice_add_mod(MidiSynth.default_chorus_mod, fluid_voice_addorover_mod.FLUID_VOICE_DEFAULT);     /* SF2.01 $8.4.9  */
            voice.fluid_voice_add_mod(MidiSynth.default_pitch_bend_mod, fluid_voice_addorover_mod.FLUID_VOICE_DEFAULT); /* SF2.01 $8.4.10 */
#if DEBUGPERF
            synth.DebugPerf("After fluid_voice_add_mod:");
#endif

            ActiveVoices.Add(voice);
            voice.IndexActive = ActiveVoices.Count - 1;

            MPTK_StatVoiceCountActive = ActiveVoices.Count;
            MPTK_StatVoiceCountFree = FreeVoices.Count;
            //if (countvoiceAllocated > 0) cant'be zero
            MPTK_StatVoiceRatioReused = (countvoiceReused * 100f) / MPTK_StatVoicePlayed;
            return voice;
        }

        public void fluid_synth_kill_by_exclusive_class(fluid_voice new_voice)
        {
            //fluid_synth_t* synth
            /** Kill all voices on a given channel, which belong into
                excl_class.  This function is called by a SoundFont's preset in
                response to a noteon event.  If one noteon event results in
                several voice processes (stereo samples), ignore_ID must name
                the voice ID of the first generated voice (so that it is not
                stopped). The first voice uses ignore_ID=-1, which will
                terminate all voices on a channel belonging into the exclusive
                class excl_class.
            */

            //int i;
            int excl_class = (int)new_voice.gens[(int)fluid_gen_type.GEN_EXCLUSIVECLASS].Val;
            /* Check if the voice belongs to an exclusive class. In that case, previous notes from the same class are released. */

            /* Excl. class 0: No exclusive class */
            if (excl_class == 0)
            {
                return;
            }

            //  FLUID_LOG(FLUID_INFO, "Voice belongs to exclusive class (class=%d, ignore_id=%d)", excl_class, ignore_ID);

            /* Kill all notes on the same channel with the same exclusive class */

            for (int i = 0; i < ActiveVoices.Count; i++)
            {
                fluid_voice voice = ActiveVoices[i];
                /* Existing voice does not play? Leave it alone. */
                if (!(voice.status == fluid_voice_status.FLUID_VOICE_ON) || voice.status == fluid_voice_status.FLUID_VOICE_SUSTAINED)
                {
                    continue;
                }

                /* An exclusive class is valid for a whole channel (or preset). Is the voice on a different channel? Leave it alone. */
                if (voice.chan != new_voice.chan)
                {
                    continue;
                }

                /* Existing voice has a different (or no) exclusive class? Leave it alone. */
                if ((int)new_voice.gens[(int)fluid_gen_type.GEN_EXCLUSIVECLASS].Val != excl_class)
                {
                    continue;
                }

                /* Existing voice is a voice process belonging to this noteon event (for example: stereo sample)?  Leave it alone. */
                if (voice.IdVoice == new_voice.IdVoice)
                {
                    continue;
                }

                //    FLUID_LOG(FLUID_INFO, "Releasing previous voice of exclusive class (class=%d, id=%d)",
                //     (int)_GEN(existing_voice, GEN_EXCLUSIVECLASS), (int)fluid_voice_get_id(existing_voice));

                voice.fluid_voice_kill_excl();
            }
        }
        /// <summary>
        ///  Start a synthesis voice. This function is called by a soundfont's preset in response to a noteon event after the voice  has been allocated with fluid_synth_alloc_voice() and initialized.
        /// Exclusive classes are processed here.
        /// </summary>
        /// <param name="synth"></param>
        /// <param name="voice"></param>

        public void fluid_synth_start_voice(fluid_voice voice)
        {
            //fluid_synth_t synth
            /*   fluid_mutex_lock(synth.busy); /\* Don't interfere with the audio thread *\/ */
            /*   fluid_mutex_unlock(synth.busy); */

            /* Find the exclusive class of this voice. If set, kill all voices
             * that match the exclusive class and are younger than the first
             * voice process created by this noteon event. */
            fluid_synth_kill_by_exclusive_class(voice);

            /* Start the new voice */
            voice.fluid_voice_start();
        }

        public HiPreset fluid_synth_find_preset(int banknum, int prognum)
        {
            ImSoundFont sfont = MidiPlayerGlobal.ImSFCurrent;
            if (banknum >= 0 && banknum < sfont.Banks.Length &&
                sfont.Banks[banknum] != null &&
                sfont.Banks[banknum].defpresets != null &&
                prognum < sfont.Banks[banknum].defpresets.Length &&
                sfont.Banks[banknum].defpresets[prognum] != null)
                return sfont.Banks[banknum].defpresets[prognum];

            // Not find, return the first available
            foreach (ImBank bank in sfont.Banks)
                if (bank != null)
                    foreach (HiPreset preset in bank.defpresets)
                        if (preset != null)
                            return preset;
            return null;
        }

        public void synth_noteon(MPTKEvent note)
        {
            HiSample hiSample;
            fluid_voice voice;
            List<HiMod> mod_list = new List<HiMod>();

            int key = note.Value + MPTK_Transpose;
            int vel = note.Velocity;
            HiPreset preset;
            //DebugPerf("Begin synth_noteon:");

            if (!Channels[note.Channel].enabled)
            {
                if (MPTK_LogWave)
                    Debug.LogFormat("Channel {0} disabled, cancel playing note: {1}", note.Channel, note.Value);
                return;
            }

            // Use the preset defined in the channel
            preset = Channels[note.Channel].preset;
            if (preset == null)
            {
                Debug.LogWarningFormat("No preset associated to this channel {0}, cancel playing note: {1}", note.Channel, note.Value);
                return;
            }

            fluid_synth_release_voice_on_same_note(note.Channel, key);

            ImSoundFont sfont = MidiPlayerGlobal.ImSFCurrent;
            note.Voices = new List<fluid_voice>();

            // run thru all the zones of this preset 
            foreach (HiZone preset_zone in preset.Zone)
            {
                // check if the note falls into the key and velocity range of this preset 
                if ((preset_zone.KeyLo <= key) &&
                    (preset_zone.KeyHi >= key) &&
                    (preset_zone.VelLo <= vel) &&
                    (preset_zone.VelHi >= vel))
                {
                    if (preset_zone.Index >= 0)
                    {
                        HiInstrument inst = sfont.HiSf.inst[preset_zone.Index];
                        HiZone global_inst_zone = inst.GlobalZone;

                        // run thru all the zones of this instrument */
                        foreach (HiZone inst_zone in inst.Zone)
                        {

                            if (inst_zone.Index < 0 || inst_zone.Index >= sfont.HiSf.Samples.Length)
                                continue;

                            // make sure this instrument zone has a valid sample
                            hiSample = sfont.HiSf.Samples[inst_zone.Index];
                            if (hiSample == null)
                                continue;

                            // check if the note falls into the key and velocity range of this instrument

                            if ((inst_zone.KeyLo <= key) &&
                                (inst_zone.KeyHi >= key) &&
                                (inst_zone.VelLo <= vel) &&
                                (inst_zone.VelHi >= vel))
                            {
                                //
                                // Find a sample to play
                                //
                                //Debug.Log("   Found Instrument '" + inst.name + "' index:" + inst_zone.index + " '" + sfont.hisf.Samples[inst_zone.index].Name + "'");
                                //DebugPerf("After found instrument:");

                                voice = fluid_synth_alloc_voice(hiSample, note.Channel, key, vel);
#if DEBUGPERF
                                DebugPerf("After fluid_synth_alloc_voice:");
#endif

                                if (voice == null) return;


                                note.Voices.Add(voice);
                                voice.Duration = note.Duration;
                                voice.DurationTick = note.Duration * fluid_voice.Nano100ToMilli;

                                //
                                // Instrument level - Generator
                                // ----------------------------

                                // Global zone

                                // SF 2.01 section 9.4 'bullet' 4: A generator in a local instrument zone supersedes a global instrument zone generator.  
                                // Both cases supersede the default generator. The generator not defined in this instrument do nothing, leave it at the default.

                                if (global_inst_zone != null && global_inst_zone.gens != null)
                                    foreach (HiGen gen in global_inst_zone.gens)
                                    {
                                        //fluid_voice_gen_set(voice, i, global_inst_zone.gen[i].val);
                                        voice.gens[(int)gen.type].Val = gen.Val;
                                        voice.gens[(int)gen.type].flags = fluid_gen_flags.GEN_SET_INSTRUMENT;
                                    }

                                // Local zone
                                if (inst_zone.gens != null && inst_zone.gens != null)
                                    foreach (HiGen gen in inst_zone.gens)
                                    {
                                        //fluid_voice_gen_set(voice, i, global_inst_zone.gen[i].val);
                                        voice.gens[(int)gen.type].Val = gen.Val;
                                        voice.gens[(int)gen.type].flags = fluid_gen_flags.GEN_SET_INSTRUMENT;
                                    }

                                //
                                // Instrument level - Modulators
                                // -----------------------------

                                /// Global zone
                                mod_list = new List<HiMod>();
                                if (global_inst_zone != null && global_inst_zone.mods != null)
                                {
                                    foreach (HiMod mod in global_inst_zone.mods)
                                        mod_list.Add(mod);
                                    //HiMod.DebugLog("      Instrument Global Mods ", global_inst_zone.mods);
                                }
                                //HiMod.DebugLog("      Instrument Local Mods ", inst_zone.mods);

                                // Local zone
                                if (inst_zone.mods != null)
                                    foreach (HiMod mod in inst_zone.mods)
                                    {
                                        // 'Identical' modulators will be deleted by setting their list entry to NULL.  The list length is known. 
                                        // NULL entries will be ignored later.  SF2.01 section 9.5.1 page 69, 'bullet' 3 defines 'identical'.

                                        foreach (HiMod mod1 in mod_list)
                                        {
                                            // fluid_mod_test_identity(mod, mod_list[i]))
                                            if ((mod1.Dest == mod.Dest) &&
                                                (mod1.Src1 == mod.Src1) &&
                                                (mod1.Src2 == mod.Src2) &&
                                                (mod1.Flags1 == mod.Flags1) &&
                                                (mod1.Flags2 == mod.Flags2))
                                            {
                                                mod1.Amount = mod.Amount;
                                                break;
                                            }
                                        }
                                    }

                                // Add instrument modulators (global / local) to the voice.
                                // Instrument modulators -supersede- existing (default) modulators.  SF 2.01 page 69, 'bullet' 6
                                foreach (HiMod mod1 in mod_list)
                                    voice.fluid_voice_add_mod(mod1, fluid_voice_addorover_mod.FLUID_VOICE_OVERWRITE);

                                //
                                // Preset level - Generators
                                // -------------------------

                                //  Local zone
                                if (preset_zone.gens != null)
                                    foreach (HiGen gen in preset_zone.gens)
                                    {
                                        //fluid_voice_gen_incr(voice, i, preset.global_zone.gen[i].val);
                                        //if (gen.type==fluid_gen_type.GEN_VOLENVATTACK)
                                        voice.gens[(int)gen.type].Val += gen.Val;
                                        voice.gens[(int)gen.type].flags = fluid_gen_flags.GEN_SET_PRESET;
                                    }

                                // Global zone
                                if (preset.GlobalZone != null && preset.GlobalZone.gens != null)
                                {
                                    foreach (HiGen gen in preset.GlobalZone.gens)
                                    {
                                        // If not incremented in local, increment in global
                                        if (voice.gens[(int)gen.type].flags != fluid_gen_flags.GEN_SET_PRESET)
                                        {
                                            //fluid_voice_gen_incr(voice, i, preset.global_zone.gen[i].val);
                                            voice.gens[(int)gen.type].Val += gen.Val;
                                            voice.gens[(int)gen.type].flags = fluid_gen_flags.GEN_SET_PRESET;
                                        }
                                    }
                                }


                                //
                                // Preset level - Modulators
                                // -------------------------

                                // Global zone
                                mod_list = new List<HiMod>();
                                if (preset.GlobalZone != null && preset.GlobalZone.mods != null)
                                {
                                    foreach (HiMod mod in preset.GlobalZone.mods)
                                        mod_list.Add(mod);
                                    //HiMod.DebugLog("      Preset Global Mods ", preset.global_zone.mods);
                                }
                                //HiMod.DebugLog("      Preset Local Mods ", preset_zone.mods);

                                // Local zone
                                if (preset_zone.mods != null)
                                    foreach (HiMod mod in preset_zone.mods)
                                    {
                                        // 'Identical' modulators will be deleted by setting their list entry to NULL.  The list length is known. 
                                        // NULL entries will be ignored later.  SF2.01 section 9.5.1 page 69, 'bullet' 3 defines 'identical'.

                                        foreach (HiMod mod1 in mod_list)
                                        {
                                            // fluid_mod_test_identity(mod, mod_list[i]))
                                            if ((mod1.Dest == mod.Dest) &&
                                                (mod1.Src1 == mod.Src1) &&
                                                (mod1.Src2 == mod.Src2) &&
                                                (mod1.Flags1 == mod.Flags1) &&
                                                (mod1.Flags2 == mod.Flags2))
                                            {
                                                mod1.Amount = mod.Amount;
                                                break;
                                            }
                                        }
                                    }

                                // Add preset modulators (global / local) to the voice.
                                foreach (HiMod mod1 in mod_list)
                                    if (mod1.Amount != 0d)
                                        // Preset modulators -add- to existing instrument default modulators.  
                                        // SF2.01 page 70 first bullet on page 
                                        voice.fluid_voice_add_mod(mod1, fluid_voice_addorover_mod.FLUID_VOICE_ADD);

#if DEBUGPERF
                                DebugPerf("After genmod init:");
#endif

                                /* add the synthesis process to the synthesis loop. */
                                //fluid_synth_t synth
                                /*   fluid_mutex_lock(synth.busy); /\* Don't interfere with the audio thread *\/ */
                                /*   fluid_mutex_unlock(synth.busy); */

                                // Start the new voice 
                                fluid_synth_start_voice(voice);

#if DEBUGPERF
                                DebugPerf("After fluid_voice_start:");
#endif

                                if (MPTK_LogWave)
                                    Debug.LogFormat("NoteOn [C:{0:00} B:{1:000} P:{2:000}]\t{3,-21}\tKey:{4,-3}\tVel:{5,-3}\tDuration:{6:0.000}\tInstr:{7,-21}\t\tWave:{8,-21}\tAtt:{9:0.00}\tPan:{10:0.00}",
                                    note.Channel + 1, Channels[note.Channel].banknum, Channels[note.Channel].prognum, preset.Name, key, vel, note.Duration,
                                    inst.Name,
                                    sfont.HiSf.Samples[inst_zone.Index].Name,
                                    fluid_conv.fluid_atten2amp(voice.attenuation),
                                    voice.pan
                                );

                                if (VerboseGenerator)
                                    foreach (HiGen gen in voice.gens)
                                        if (gen != null && gen.flags > 0)
                                            Debug.LogFormat("Gen Id:{1,-50}\t{0}\tValue:{2:0.00}\tMod:{3:0.00}\tflags:{4,-50}", (int)gen.type, gen.type, gen.Val, gen.Mod, gen.flags);

                                /* Store the ID of the first voice that was created by this noteon event.
                                 * Exclusive class may only terminate older voices.
                                 * That avoids killing voices, which have just been created.
                                 * (a noteon event can create several voice processes with the same exclusive
                                 * class - for example when using stereo samples)
                                 */
                            }
                            if (MPTK_PlayOnlyFirstWave && note.Voices.Count > 0)
                                return;
                        }
                    }

                }
            }
#if DEBUGPERF
            DebugPerf("After synth_noteon:");
#endif
            if (MPTK_LogWave && note.Voices.Count == 0)
                Debug.LogFormat("NoteOn [{0:00} {1:000} {2:000}]\t{3,-21}\tKey:{4,-3}\tVel:{5,-3}\tDuration:{6:0.000}\tInstr:{7,-21}",
                note.Channel, Channels[note.Channel].banknum, Channels[note.Channel].prognum, preset.Name, key, vel, note.Duration, "*** no wave found ***");
        }

        // If the same note is hit twice on the same channel, then the older voice process is advanced to the release stage.  
        // Using a mechanical MIDI controller, the only way this can happen is when the sustain pedal is held.
        // In this case the behaviour implemented here is natural for many instruments.  
        // Note: One noteon event can trigger several voice processes, for example a stereo sample.  Don't release those...
        void fluid_synth_release_voice_on_same_note(int chan, int key)
        {
            foreach (fluid_voice voice in ActiveVoices)
            {
                if (voice.chan == chan && voice.key == key)
                //&& (fluid_voice_get_id(voice) != synth->noteid))
                {
                    voice.fluid_voice_noteoff(true);
                    // can't break, beacause need to search in case of multi sample
                }
            }
        }


        public void fluid_synth_allnotesoff()
        {
            for (int chan = 0; chan < Channels.Length; chan++)
                fluid_synth_noteoff(chan, -1);
        }

        public void fluid_synth_noteoff(int pchan, int pkey)
        {
            for (int i = 0; i < ActiveVoices.Count; i++)
            {
                fluid_voice voice = ActiveVoices[i];
                // A voice is 'ON', if it has not yet received a noteoff event. Sending a noteoff event will advance the envelopes to  section 5 (release). 
                //#define _ON(voice)  ((voice)->status == FLUID_VOICE_ON && (voice)->volenv_section < FLUID_VOICE_ENVRELEASE)
                if (voice.status == fluid_voice_status.FLUID_VOICE_ON &&
                    voice.volenv_section < fluid_voice_envelope_index.FLUID_VOICE_ENVRELEASE &&
                    voice.chan == pchan &&
                    (pkey == -1 || voice.key == pkey))
                {
                    //fluid_global.FLUID_LOG(fluid_log_level.FLUID_INFO, "noteoff chan:{0} key:{1} vel:{2} time{3}", voice.chan, voice.key, voice.vel, (fluid_curtime() - start) / 1000.0f);
                    voice.fluid_voice_noteoff();
                }
            }
        }

        public void fluid_synth_soundoff(int pchan)
        {
            for (int i = 0; i < ActiveVoices.Count; i++)
            {
                fluid_voice voice = ActiveVoices[i];
                // A voice is 'ON', if it has not yet received a noteoff event. Sending a noteoff event will advance the envelopes to  section 5 (release). 
                //#define _ON(voice)  ((voice)->status == FLUID_VOICE_ON && (voice)->volenv_section < FLUID_VOICE_ENVRELEASE)
                if (voice.status == fluid_voice_status.FLUID_VOICE_ON &&
                    voice.volenv_section < fluid_voice_envelope_index.FLUID_VOICE_ENVRELEASE &&
                    voice.chan == pchan)
                {
                    //fluid_global.FLUID_LOG(fluid_log_level.FLUID_INFO, "noteoff chan:{0} key:{1} vel:{2} time{3}", voice.chan, voice.key, voice.vel, (fluid_curtime() - start) / 1000.0f);
                    voice.fluid_voice_off();
                }
            }
        }

        /*
         * fluid_synth_damp_voices
         */
        public void fluid_synth_damp_voices(int pchan)
        {
            for (int i = 0; i < ActiveVoices.Count; i++)
            {
                fluid_voice voice = ActiveVoices[i];
                //#define _SUSTAINED(voice)  ((voice)->status == FLUID_VOICE_SUSTAINED)
                if (voice.chan == pchan && voice.status == fluid_voice_status.FLUID_VOICE_SUSTAINED)
                    voice.fluid_voice_noteoff(true);
            }
        }

        /*
         * fluid_synth_cc - call directly
         */
        public void fluid_synth_cc(int chan, MPTKController num, int val)
        {
            /*   fluid_mutex_lock(busy); /\* Don't interfere with the audio thread *\/ */
            /*   fluid_mutex_unlock(busy); */

            /* check the ranges of the arguments */
            //if ((chan < 0) || (chan >= midi_channels))
            //{
            //    FLUID_LOG(FLUID_WARN, "Channel out of range");
            //    return FLUID_FAILED;
            //}
            //if ((num < 0) || (num >= 128))
            //{
            //    FLUID_LOG(FLUID_WARN, "Ctrl out of range");
            //    return FLUID_FAILED;
            //}
            //if ((val < 0) || (val >= 128))
            //{
            //    FLUID_LOG(FLUID_WARN, "Value out of range");
            //    return FLUID_FAILED;
            //}

            /* set the controller value in the channel */
            Channels[chan].fluid_channel_cc(num, val);
        }

        /// <summary>
        /// tell all synthesis activ voices on this channel to update their synthesis parameters after a control change.
        /// </summary>
        /// <param name="chan"></param>
        /// <param name="is_cc"></param>
        /// <param name="ctrl"></param>
        public void fluid_synth_modulate_voices(int chan, int is_cc, int ctrl)
        {
            for (int i = 0; i < ActiveVoices.Count; i++)
            {
                fluid_voice voice = ActiveVoices[i];
                if (voice.chan == chan && voice.status != fluid_voice_status.FLUID_VOICE_OFF)
                    voice.fluid_voice_modulate(is_cc, ctrl);
            }
        }

        /// <summary>
        /// Tell all synthesis processes on this channel to update their synthesis parameters after an all control off message (i.e. all controller have been reset to their default value).
        /// </summary>
        /// <param name="chan"></param>
        public void fluid_synth_modulate_voices_all(int chan)
        {
            for (int i = 0; i < ActiveVoices.Count; i++)
            {
                fluid_voice voice = ActiveVoices[i];
                if (voice.chan == chan)
                    voice.fluid_voice_modulate_all();
            }
        }

        /*
         * fluid_synth_program_change
         */
        public void fluid_synth_program_change(int pchan, int prognum)
        {
            fluid_channel channel;
            HiPreset preset;
            int banknum;

            channel = Channels[pchan];
            banknum = channel.banknum; //fluid_channel_get_banknum
            channel.prognum = prognum; // fluid_channel_set_prognum
            if (VerboseVoice) Debug.LogFormat("ProgramChange\tChannel:{0}\tBank:{1}\tPreset:{2}", pchan, banknum, prognum);
            preset = fluid_synth_find_preset(banknum, prognum);
            channel.preset = preset; // fluid_channel_set_preset
        }


        /*
         * fluid_synth_pitch_bend
         */
        void fluid_synth_pitch_bend(int chan, int val)
        {
            if (MPTK_ApplyRealTimeModulator)
            {
                /*   fluid_mutex_lock(busy); /\* Don't interfere with the audio thread *\/ */
                /*   fluid_mutex_unlock(busy); */

                /* check the ranges of the arguments */
                if (chan < 0 || chan >= Channels.Length)
                {
                    Debug.LogFormat("Channel out of range chan:{0}", chan);
                    return;
                }

                /* set the pitch-bend value in the channel */
                Channels[chan].fluid_channel_pitch_bend(val);
            }
        }

        /// <summary>
        /// Play a list of Midi events 
        /// </summary>
        /// <param name="midievents">List of Midi events to play</param>
        protected void PlayEvents(List<MPTKEvent> midievents)
        {
            if (MidiPlayerGlobal.MPTK_SoundFontLoaded == false)
                return;

            if (midievents != null && midievents.Count < 100)
            {
                foreach (MPTKEvent note in midievents)
                {
#if DEBUGPERF
                    DebugPerf("-----> Init perf:", 0);
#endif
                    //float beforePLay = Time.realtimeSinceStartup;
                    PlayEvent(note);
                    //Debug.Log("Elapsed:" + (Time.realtimeSinceStartup - beforePLay) * 1000f);
#if DEBUGPERF
                    DebugPerf("<---- ClosePerf perf:", 2);
#endif
                }
            }
        }
#if DEBUGNOTE
        public int numberNote = -1;
        public int startNote;
        public int countNote;
#endif
        /// <summary>
        /// Play one Midi event
        /// @snippet MusicView.cs Example PlayNote
        /// </summary>
        /// <param name="midievent"></param>
        protected void StopEvent(MPTKEvent midievent)
        {
            try
            {
                if (midievent != null && midievent.Voices != null)
                {
                    for (int i = 0; i < midievent.Voices.Count; i++)
                    {
                        fluid_voice voice = midievent.Voices[i];
                        if (voice.volenv_section != fluid_voice_envelope_index.FLUID_VOICE_ENVRELEASE &&
                            voice.status != fluid_voice_status.FLUID_VOICE_OFF)
                            voice.fluid_voice_noteoff();
                    }
                }
            }
            catch (System.Exception ex)
            {
                MidiPlayerGlobal.ErrorDetail(ex);
            }
        }

        /// <summary>
        /// Play one Midi event
        /// @snippet MusicView.cs Example PlayNote
        /// </summary>
        /// <param name="midievent"></param>
        protected void PlayEvent(MPTKEvent midievent)
        {
            try
            {
                if (MidiPlayerGlobal.CurrentMidiSet == null || MidiPlayerGlobal.CurrentMidiSet.ActiveSounFontInfo == null)
                {
                    Debug.Log("No SoundFont selected for MPTK_PlayNote ");
                    return;
                }

                switch (midievent.Command)
                {
                    case MPTKCommand.NoteOn:
                        if (midievent.Velocity != 0)
                        {
#if DEBUGNOTE
                            numberNote++;
                            if (numberNote < startNote || numberNote > startNote + countNote - 1) return;
#endif
                            //if (note.Channel==4)
                            synth_noteon(midievent);
                        }
                        break;

                    case MPTKCommand.NoteOff:
                        fluid_synth_noteoff(midievent.Channel, midievent.Value);
                        break;

                    case MPTKCommand.ControlChange:
                        if (MPTK_ApplyRealTimeModulator)
                            Channels[midievent.Channel].fluid_channel_cc(midievent.Controller, midievent.Value); // replace of fluid_synth_cc(note.Channel, note.Controller, (int)note.Value);
                        break;

                    case MPTKCommand.PatchChange:
                        if (midievent.Channel != 9 || MPTK_EnablePresetDrum == true)
                            fluid_synth_program_change(midievent.Channel, midievent.Value);
                        break;

                    case MPTKCommand.PitchWheelChange:
                        fluid_synth_pitch_bend(midievent.Channel, midievent.Value);
                        break;
                }
            }
            catch (System.Exception ex)
            {
                MidiPlayerGlobal.ErrorDetail(ex);
            }
        }

#if DEBUGOnAudioFilterRead
        private double lastTimeDSP;
        public int deltaTimeDSP;
        private long lastTimeTicks;
        public int deltaTimeTicks;
#endif

        private void OnAudioFilterRead(float[] data, int channels)
        {

#if DEBUGOnAudioFilterRead
            double newTimeDSP = AudioSettings.dspTime;
            deltaTimeDSP = (int)((newTimeDSP - lastTimeDSP) * 1000f);
            lastTimeDSP = newTimeDSP;

            long newTime = System.DateTime.Now.Ticks; // 100 nanosecondes ou 10 millions cycles dans une seconde.
            deltaTimeTicks = (int)((newTime - lastTimeTicks) / 10000F);
            lastTimeTicks = newTime;
#endif

            //This uses the Unity specific float method we added to get the buffer
            if (MPTK_CorePlayer && state == fluid_synth_status.FLUID_SYNTH_PLAYING)
            {
                //Debug.LogFormat("OnAudioFilterRead: time [{0}:{1}] {2}", System.DateTime.Now.Second, System.DateTime.Now.Millisecond, data.Length);

                //if (System.DateTime.Now.Second % 5 == 0 && System.DateTime.Now.Millisecond==0)
                //    synth_noteon(new MPTKEvent()
                //    {
                //        Channel = 0,
                //        Command = MPTKCommand.NoteOn,
                //        Value = 60,
                //        Velocity = 127,
                //        Duration = 9999999,
                //    });

                try
                {
                    while (QueueSynthCommand.Count > 0)
                    {
                        SynthCommand action = QueueSynthCommand.Dequeue();
                        switch (action.Command)
                        {
                            case SynthCommand.enCmd.StartEvent:
                                PlayEvent(action.MidiEvent);
                                break;
                            case SynthCommand.enCmd.StopEvent:
                                StopEvent(action.MidiEvent);
                                break;
                            case SynthCommand.enCmd.ClearAllVoices:
                                ActiveVoices.Clear();
                                break;
                            case SynthCommand.enCmd.NoteOffAll:
                                for (int i = 0; i < ActiveVoices.Count; i++)
                                {
                                    fluid_voice voice = ActiveVoices[i];
                                    if (voice.status == fluid_voice_status.FLUID_VOICE_ON || voice.status == fluid_voice_status.FLUID_VOICE_SUSTAINED)
                                    {
                                        //Debug.LogFormat("ReleaseAll {0} / {1}", voice.IdVoice, ActiveVoices.Count);
                                        voice.fluid_voice_noteoff(true);
                                    }
                                }
                                break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning(ex.Message);
                }

                MoveVoiceToFree();
                AutoCleanVoice();

                MPTK_StatVoiceCountActive = ActiveVoices.Count;
                MPTK_StatVoiceCountFree = FreeVoices.Count;

                if (miditoplay != null && miditoplay.ReadyToPlay)
                {
                    PlayMidi();
                }

                int block = 0;
                while (block < dspBufferSize)
                {
                    Array.Clear(left_buf, 0, FLUID_BUFSIZE);
                    Array.Clear(right_buf, 0, FLUID_BUFSIZE);

                    for (int i = 0; i < ActiveVoices.Count; i++)
                    {
                        ActiveVoices[i].fluid_voice_write(left_buf, right_buf, null /*reverb_buf*/, null /*chorus_buf*/);
                    }

                    //Debug.Log("   block:" + block + " j start:" + ((block + 0) * 2) + " j end:" + ((block + FLUID_BUFSIZE-1) * 2) + " data.Length:" + data.Length );

                    for (int i = 0; i < FLUID_BUFSIZE; i++)
                    {
                        int j = (block + i) * 2;
                        data[j + 0] = left_buf[i] * MPTK_Volume;
                        data[j + 1] = right_buf[i] * MPTK_Volume;
                    }
                    block += FLUID_BUFSIZE;
                }
            }
        }

        public void MoveVoiceToFree(fluid_voice v)
        {
            ActiveVoices.RemoveAt(v.IndexActive);
            FreeVoices.Add(v);
        }

        public void DebugVoice()
        {
            foreach (fluid_voice v in ActiveVoices)
            {
                Debug.LogFormat("", v.LastTimeWrite);
            }
        }

        private void MoveVoiceToFree()
        {
            for (int indexVoice = 0; indexVoice < ActiveVoices.Count;)
            {
                fluid_voice v = ActiveVoices[indexVoice];
                if (v.status == fluid_voice_status.FLUID_VOICE_OFF)
                {
                    ActiveVoices.RemoveAt(indexVoice);
                    FreeVoices.Add(v);
                }
                else
                {
                    indexVoice++;
                }
            }
        }

        private void AutoCleanVoice()
        {
            for (int indexVoice = 0; indexVoice < FreeVoices.Count;)
            {
                if (FreeVoices.Count > MPTK_AutoCleanVoiceLimit)
                {
                    fluid_voice voice = FreeVoices[indexVoice];
                    // Is it an older voice ?
                    //if ((Time.realtimeSinceStartup * 1000d - v.TimeAtStart) > AutoCleanVoiceTime)
                    if (((System.DateTime.Now.Ticks - voice.TimeAtStart) / fluid_voice.Nano100ToMilli) > MPTK_AutoCleanVoiceTime)
                    {
                        if (VerboseVoice) Debug.LogFormat("Remove voice total:{0} id:{1} start:{2}", FreeVoices.Count, voice.IdVoice, (System.DateTime.Now.Ticks - voice.TimeAtStart) / fluid_voice.Nano100ToMilli);
                        FreeVoices.RemoveAt(indexVoice);
                        if (voice.VoiceAudio != null) Destroy(voice.VoiceAudio.gameObject);
                    }
                    else
                        indexVoice++;
                }
                else
                    break;
            }
        }

        protected double lastMidiTimePlayAS = 0d;
        protected double lastMidiTimePlayCore = 0d;

        /// <summary>
        /// Time in millisecond from the start of play
        /// </summary>
        protected double timeMidiFromStartPlay = 0d;

        void PlayMidi()
        {
            double now = (System.DateTime.Now.Ticks / 10000D);
            double deltaTime = /*AudioSettings.dspTime*/  now - lastMidiTimePlayCore;
            lastMidiTimePlayCore = /*AudioSettings.dspTime*/now;
            timeMidiFromStartPlay += deltaTime /** 1000d*/;

            // Read midi events until this time
            List<MPTKEvent> midievents = miditoplay.ReadMidiEvents(timeMidiFromStartPlay);

            QueueMidiEvents.Enqueue(midievents);

            // Play notes read from the midi file
            if (midievents != null && midievents.Count > 0)
            {
                if (MPTK_DirectSendToPlayer)
                {
                    foreach (MPTKEvent midievent in midievents)
                    {
                        if (midievent.Command == MPTKCommand.MetaEvent && midievent.Meta == MPTKMeta.SetTempo && MPTK_EnableChangeTempo)
                        {
                            miditoplay.ChangeTempo(midievent.Duration);
                        }
                        else
                        {
                            PlayEvent(midievent);
                        }
                    }
                }
            }
        }

        //! @endcond

#if DEBUGPERF

        float perf_time_before;
        float perf_time_cumul;
        public List<string> perfs;

        public void DebugPerf(string info, int mode = 1)
        {
            // Init
            if (mode == 0)
            {
                perfs = new List<string>();
                perf_time_before = Time.realtimeSinceStartup;
                perf_time_cumul = 0;
            }
            if (perfs != null)
            {
                float delta = (Time.realtimeSinceStartup - perf_time_before) * 1000f;
                perf_time_before = Time.realtimeSinceStartup;
                perf_time_cumul += delta;
                string perf = string.Format("{0,-30} \t\t delta:{1,5:F5} ms \t cumul:{2,5:F5} ms", info, delta, perf_time_cumul);
                perfs.Add(perf);
                // Debug.Log(perf);
            }
            // Close
            if (mode == 2)
            {
                foreach (string p in perfs) Debug.Log(p);
                //Debug.Log(perfs.Last());
            }
        }
#endif
    }
}
