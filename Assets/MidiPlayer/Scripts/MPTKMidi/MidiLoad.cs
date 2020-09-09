using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using NAudio.Midi;
using System;
using System.IO;
using System.Linq;

namespace MidiPlayerTK
{
    /// <summary>
    /// Internal class for loading a Midi file. 
    /// No sequencer, no synthetizer, no music playing capabilities. 
    /// Usefull to load all the Midi events from a Midi and process, transform, write them to want you want. 
    public class MidiLoad
    {
        //! @cond NODOC
        public MidiFile midifile;
        public List<TrackMidiEvent> MidiSorted;
        public bool EndMidiEvent;
        public double QuarterPerMinuteValue;
        public string SequenceTrackName = "";
        public string ProgramName = "";
        public string TrackInstrumentName = "";
        public string TextEvent = "";
        public string Copyright = "";
        public double TickLengthMs;

        //! @endcond

        /// <summary>
        /// Initial tempo found in the Midi
        /// </summary>
        public double MPTK_InitialTempo;

        /// <summary>
        /// Duration of the midi. This duration is not constant depending of midi event change tempo inside the midi file.
        /// </summary>
        public TimeSpan MPTK_Duration;

        /// <summary>
        /// Real Duration of the midi calculated with the midi change tempo events find inside the midi file.
        /// </summary>
        public TimeSpan MPTK_RealDuration;

        private long timeLastSegment;

        /// <summary>
        /// Last tick position in Midi: Time of the last midi event in sequence expressed in number of "ticks". MPTK_TickLast / MPTK_DeltaTicksPerQuarterNote equal the duration time of a quarter-note regardless the defined tempo.
        /// </summary>
        public long MPTK_TickLast;

        /// <summary>
        /// Current tick position in Midi: Time of the current midi event expressed in number of "ticks". MPTK_TickCurrent / MPTK_DeltaTicksPerQuarterNote equal the duration time of a quarter-note regardless the defined tempo.
        /// </summary>
        public long MPTK_TickCurrent;

        /// <summary>
        /// From TimeSignature event: The numerator counts the number of beats in a measure. For example a numerator of 4 means that each bar contains four beats. This is important to know because usually the first beat of each bar has extra emphasis.
        /// http://www.deluge.co/?q=midi-tempo-bpm
        /// </summary>
        public int MPTK_NumberBeatsMeasure;

        /// <summary>
        /// From TimeSignature event: number of quarter notes in a beat. Equal 2 Power TimeSigDenominator.
        /// http://www.deluge.co/?q=midi-tempo-bpm
        /// </summary>
        public int MPTK_NumberQuarterBeat;

        /// <summary>
        /// From TimeSignature event: The numerator counts the number of beats in a measure. For example a numerator of 4 means that each bar contains four beats. This is important to know because usually the first beat of each bar has extra emphasis. In MIDI the denominator value is stored in a special format. i.e. the real denominator = 2^[dd]
        /// http://www.deluge.co/?q=midi-tempo-bpm
        /// </summary>
        public int MPTK_TimeSigNumerator;

        /// <summary>
        /// From TimeSignature event: The denominator specifies the number of quarter notes in a beat. 2 represents a quarter-note, 3 represents an eighth-note, etc. . 
        /// http://www.deluge.co/?q=midi-tempo-bpm
        /// </summary>
        public int MPTK_TimeSigDenominator;

        /// <summary>
        /// From TimeSignature event: The standard MIDI clock ticks every 24 times every quarter note (crotchet) so a [cc] value of 24 would mean that the metronome clicks once every quarter note. A [cc] value of 6 would mean that the metronome clicks once every 1/8th of a note (quaver).
        /// http://www.deluge.co/?q=midi-tempo-bpm
        /// </summary>
        public int MPTK_TicksInMetronomeClick;

        /// <summary>
        /// From TimeSignature event: This value specifies the number of 1/32nds of a note happen every MIDI quarter note. It is usually 8 which means that a quarter note happens every quarter note.
        /// http://www.deluge.co/?q=midi-tempo-bpm
        /// </summary>
        public int MPTK_No32ndNotesInQuarterNote;

        /// <summary>
        /// From the SetTempo event: The tempo is given in micro seconds per quarter beat. 
        /// To convert this to BPM we needs to use the following equation:BPM = 60,000,000/[tt tt tt]
        /// Warning: this value can change during the playing when a change tempo event is find. 
        /// http://www.deluge.co/?q=midi-tempo-bpm
        /// </summary>
        public int MPTK_MicrosecondsPerQuarterNote;

        /// <summary>
        /// From Midi Header: Delta Ticks Per Quarter Note. 
        /// Represent the duration time in "ticks" which make up a quarter-note. 
        /// For instance, if 96, then a duration of an eighth-note in the file would be 48.
        /// </summary>
        public int MPTK_DeltaTicksPerQuarterNote;

        /// <summary>
        /// Count of track read in the Midi file
        /// </summary>
        public int MPTK_TrackCount;

        public bool LogEvents;
        public bool KeepNoteOff;
        public bool ReadyToPlay;

        private long Quantization;
        private long CurrentTick;
        private double Speed = 1d;
        private double LastTimeFromStartMS;

        // <summary>
        /// Last position played by tracks
        /// </summary>
        private int NextPosEvent;
        private static string[] NoteNames = new string[] { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

        public MidiLoad()
        {
            ReadyToPlay = false;
        }

        private void Init()
        {
            MPTK_InitialTempo = -1;
            MPTK_Duration = TimeSpan.Zero;
            MPTK_RealDuration = TimeSpan.Zero;
            timeLastSegment = 0;
            MPTK_TickLast = 0;
            MPTK_TickCurrent = 0;
            MPTK_NumberBeatsMeasure = 0;
            MPTK_NumberQuarterBeat = 0;
            MPTK_TimeSigNumerator = 0;
            MPTK_TimeSigDenominator = 0;
            MPTK_TicksInMetronomeClick = 0;
            MPTK_No32ndNotesInQuarterNote = 0;
            MPTK_MicrosecondsPerQuarterNote = 0;
            MPTK_DeltaTicksPerQuarterNote = 0;
            MPTK_TrackCount = 0;
        }

        /// <summary>
        /// Load Midi from midi MPTK referential (Unity resource). 
        /// The index of the Midi file can be found in the windo "Midi File Setup". Display with menu MPTK / Midi File Setup
        /// </summary>
        /// <param name="index"></param>
        /// <param name="strict">If true will error on non-paired note events, default:false</param>
        /// <returns>true if loaded</returns>
        public bool MPTK_Load(int index, bool strict = false)
        {
            //! @code
            // public MidiLoad MidiLoaded;
            // // .....
            // MidiLoaded = new MidiLoad();
            // MidiLoaded.MPTK_Load(14) // index for "Beattles - Michelle"
            // Debug.Log("Duration:" + MidiLoaded.MPTK_Duration);
            //! @endcode
            Init();
            bool ok = true;
            try
            {
                if (MidiPlayerGlobal.CurrentMidiSet.MidiFiles != null && MidiPlayerGlobal.CurrentMidiSet.MidiFiles.Count > 0)
                {
                    if (index >= 0 && index < MidiPlayerGlobal.CurrentMidiSet.MidiFiles.Count)
                    {
                        string midiname = MidiPlayerGlobal.CurrentMidiSet.MidiFiles[index];
                        TextAsset mididata = Resources.Load<TextAsset>(Path.Combine(MidiPlayerGlobal.MidiFilesDB, midiname));
                        midifile = new MidiFile(mididata.bytes, strict);
                        if (midifile != null)
                            AnalyseMidi();
                    }
                    else
                    {
                        Debug.LogWarningFormat("MidiLoad - index {0} out of range ", index);
                        ok = false;
                    }
                }
                else
                {
                    Debug.LogWarningFormat("MidiLoad - index:{0} - {1}", index, MidiPlayerGlobal.ErrorNoMidiFile);
                    ok = false;
                }
            }
            catch (System.Exception ex)
            {
                MidiPlayerGlobal.ErrorDetail(ex);
                ok = false;
            }
            return ok;
        }
        /// <summary>
        /// Load Midi from a local file
        /// </summary>
        /// <param name="datamidi">byte arry midi</param>
        /// <returns>true if loaded</returns>
        public bool MPTK_LoadFile(string filename, bool strict = false)
        {
            bool ok = true;
            try
            {
                using (Stream sfFile = new FileStream(filename, FileMode.Open, FileAccess.Read))
                {
                    byte[] data = new byte[sfFile.Length];
                    sfFile.Read(data, 0, (int)sfFile.Length);
                    ok = MPTK_Load(data);
                }
            }
            catch (System.Exception ex)
            {
                MidiPlayerGlobal.ErrorDetail(ex);
                ok = false;
            }
            return ok;
        }
        /// <summary>
        /// Load Midi from an array of bytes
        /// </summary>
        /// <param name="datamidi">byte arry midi</param>
        /// <param name="strict">If true will error on non-paired note events, default:false</param>
        /// <returns>true if loaded</returns>
        public bool MPTK_Load(byte[] datamidi, bool strict = false)
        {
            Init();
            bool ok = true;
            try
            {
                midifile = new MidiFile(datamidi, strict);
                if (midifile != null)
                    AnalyseMidi();
                else
                    ok = false;
            }
            catch (System.Exception ex)
            {
                MidiPlayerGlobal.ErrorDetail(ex);
                ok = false;
            }
            return ok;
        }

        /// <summary>
        /// Load Midi from a Midi file from Unity resources. The Midi file must be present in Unity MidiDB ressource folder.
        /// </summary>
        /// <param name="midiname">Midi file name without path and extension</param>
        /// <param name="strict">if true, check strict compliance with the Midi norm</param>
        /// <returns>true if loaded</returns>
        public bool MPTK_Load(string midiname, bool strict = false)
        {
            //! @code
            // public MidiLoad MidiLoaded;
            // // .....
            // MidiLoaded = new MidiLoad();
            // MidiLoaded.MPTK_Load("Beattles - Michelle")
            // Debug.Log("Duration:" + MidiLoaded.MPTK_Duration);
            //! @endcode
            try
            {
                TextAsset mididata = Resources.Load<TextAsset>(Path.Combine(MidiPlayerGlobal.MidiFilesDB, midiname));
                if (mididata != null && mididata.bytes != null && mididata.bytes.Length > 0)
                    return MPTK_Load(mididata.bytes, strict);
                else
                    Debug.LogWarningFormat("Midi {0} not loaded from folder {1}", midiname, MidiPlayerGlobal.MidiFilesDB);
            }
            catch (System.Exception ex)
            {
                MidiPlayerGlobal.ErrorDetail(ex);
            }
            return false;
        }

        /// <summary>
        /// Read the list of midi events available in the Midi from a ticks position to an end position.
        /// </summary>
        /// <param name="fromTicks">ticks start</param>
        /// <param name="toTicks">ticks end</param>
        /// <returns></returns>
        public List<MPTKEvent> MPTK_ReadMidiEvents(long fromTicks = 0, long toTicks = long.MaxValue)
        {
            List<MPTKEvent> midievents = new List<MPTKEvent>();
            try
            {
                if (midifile != null)
                {
                    foreach (TrackMidiEvent trackEvent in MidiSorted)
                    {
                        if (Quantization != 0)
                            trackEvent.AbsoluteQuantize = ((trackEvent.Event.AbsoluteTime + Quantization / 2) / Quantization) * Quantization;
                        else
                            trackEvent.AbsoluteQuantize = trackEvent.Event.AbsoluteTime;

                        //Debug.Log("ReadMidiEvents - timeFromStartMS:" + Convert.ToInt32(timeFromStartMS) + " LastTimeFromStartMS:" + Convert.ToInt32(LastTimeFromStartMS) + " CurrentPulse:" + CurrentPulse + " AbsoluteQuantize:" + trackEvent.AbsoluteQuantize);

                        if (trackEvent.AbsoluteQuantize >= fromTicks && trackEvent.AbsoluteQuantize <= toTicks)
                        {
                            ConvertToEvent(midievents, trackEvent);
                        }
                        if (trackEvent.AbsoluteQuantize > toTicks)
                            break;
                        //MPTK_TickCurrent = trackEvent.AbsoluteQuantize;
                        //MPTK_TickLast = trackEvent.AbsoluteQuantize;

                    }
                }
            }
            catch (System.Exception ex)
            {
                MidiPlayerGlobal.ErrorDetail(ex);
            }
            return midievents;
        }

        /// <summary>
        /// Convert the tick duration to a real time duration in millisecond regarding the current tempo.
        /// </summary>
        /// <param name="tick">duration in ticks</param>
        /// <returns>duration in milliseconds</returns>
        public double MPTK_ConvertTickToTime(long tick)
        {
            return tick * TickLengthMs;
        }

        /// <summary>
        /// Convert a real time duration in millisecond to a number of tick regarding the current tempo.
        /// </summary>
        /// <param name="time">duration in milliseconds</param>
        /// <returns>duration in ticks</returns>
        public long MPTK_ConvertTimeToTick(double time)
        {
            if (TickLengthMs != 0d)
                return Convert.ToInt64(time / TickLengthMs);
            else
                return 0;
        }

        // No doc until end of file
        //! @cond NODOC

        /// <summary>
        /// Build OS path to the midi file
        /// </summary>
        /// <param name="filename"></param>
        /// <returns></returns>
        static public string BuildOSPath(string filename)
        {
            try
            {
                string pathMidiFolder = Path.Combine(Application.dataPath, MidiPlayerGlobal.PathToMidiFile);
                string pathfilename = Path.Combine(pathMidiFolder, filename + MidiPlayerGlobal.ExtensionMidiFile);
                return pathfilename;
            }
            catch (System.Exception ex)
            {
                MidiPlayerGlobal.ErrorDetail(ex);
            }
            return null;
        }

        private void AnalyseMidi()
        {
            try
            {
                MPTK_TickLast = 0;
                MPTK_TickCurrent = 0;
                CurrentTick = 0;
                NextPosEvent = 0;
                LastTimeFromStartMS = 0;
                QuarterPerMinuteValue = double.NegativeInfinity;

                SequenceTrackName = "";
                ProgramName = "";
                TrackInstrumentName = "";
                TextEvent = "";
                Copyright = "";

                // Get midi events from midifile.Events
                MidiSorted = GetEvents();
                ChangeTempo(MPTK_InitialTempo);

                // If there is no tempo event, set a default value
                if (QuarterPerMinuteValue < 0d)
                    ChangeTempo(120d);
                //Debug.Log("MPTK_InitialTempo:" + MPTK_InitialTempo);

                MPTK_DeltaTicksPerQuarterNote = midifile.DeltaTicksPerQuarterNote;

            }
            catch (System.Exception ex)
            {
                MidiPlayerGlobal.ErrorDetail(ex);
            }
        }

        private List<TrackMidiEvent> GetEvents()
        {
            int countTracks = 0;

            try
            {
                List<TrackMidiEvent> events = new List<TrackMidiEvent>();
                foreach (IList<MidiEvent> track in midifile.Events)
                {
                    countTracks++;
                    foreach (MidiEvent e in track)
                    {
                        try
                        {
                            bool keepEvent = false;
                            switch (e.CommandCode)
                            {
                                case MidiCommandCode.NoteOn:
                                    //Debug.Log("NoteOn "+ KeepNoteOff);
                                    if (KeepNoteOff)
                                        // keep event il all ases, note even if no offevent defined in the noteon event. 
                                        // The note off depend on the note off not from the duration.
                                        keepEvent = true;
                                    else if (((NoteOnEvent)e).OffEvent != null)
                                        // NoteOn and NoteOff have been joined in one event by NAudio
                                        keepEvent = true;
                                    break;
                                case MidiCommandCode.NoteOff:
                                    //Debug.Log("NoteOff "+ KeepNoteOff);
                                    if (KeepNoteOff)
                                        keepEvent = true;
                                    break;
                                case MidiCommandCode.ControlChange:
                                    //ControlChangeEvent ctrl = (ControlChangeEvent)e;
                                    //Debug.Log("NoteOff");
                                    keepEvent = true;
                                    break;
                                case MidiCommandCode.PatchChange:
                                    keepEvent = true;
                                    break;

                                case MidiCommandCode.MetaEvent:
                                    MetaEvent meta = (MetaEvent)e;
                                    switch (meta.MetaEventType)
                                    {
                                        case MetaEventType.SetTempo:
                                            // Calculate the real duration
                                            MPTK_RealDuration += TimeSpan.FromMilliseconds((e.AbsoluteTime - timeLastSegment) * TickLengthMs);
                                            timeLastSegment = e.AbsoluteTime;
                                            //Debug.Log("Partial at: " + timeLastSegment + " " + MPTK_RealDuration + " " + Math.Round(TickLengthMs, 2) + " " + Math.Round(QuarterPerMinuteValue, 2));

                                            //Debug.Log("Tempo: " + ((TempoEvent)e).Tempo + " MPTK_InitialTempo:" + MPTK_InitialTempo);
                                            // Set the first tempo value find
                                            if (MPTK_InitialTempo < 0) MPTK_InitialTempo = ((TempoEvent)e).Tempo;
                                            ChangeTempo(((TempoEvent)e).Tempo);
                                            if (MPTK_MicrosecondsPerQuarterNote == 0) MPTK_MicrosecondsPerQuarterNote = ((TempoEvent)e).MicrosecondsPerQuarterNote;
                                            break;

                                        case MetaEventType.TimeSignature:
                                            AnalyzeTimeSignature(meta);
                                            break;
                                    }
                                    keepEvent = true;
                                    break;
                            }
                            if (keepEvent)
                                events.Add(new TrackMidiEvent() { IndexTrack = countTracks, Event = e.Clone() });
                        }
                        catch (System.Exception ex)
                        {
                            MidiPlayerGlobal.ErrorDetail(ex);
                            //List<TrackMidiEvent> MidiSorted = events.OrderBy(o => o.Event.AbsoluteTime).ToList();
                            return events.OrderBy(o => o.Event.AbsoluteTime).ToList();
                        }
                    }
                }

                MPTK_TrackCount = countTracks;
                /// Sort midi event by time
                List<TrackMidiEvent> midievents = events.OrderBy(o => o.Event.AbsoluteTime).ToList();
                if (midievents.Count > 0)
                {
                    long lastAbsoluteTime = midievents[midievents.Count - 1].Event.AbsoluteTime;
                    MPTK_TickLast = lastAbsoluteTime;
                    // Calculate the real duration
                    MPTK_RealDuration += TimeSpan.FromMilliseconds((lastAbsoluteTime - timeLastSegment) * TickLengthMs);
                    //Debug.Log("End at: " + lastAbsoluteTime + " " + MPTK_RealDuration + " " + Math.Round(TickLengthMs, 2) + " " + Math.Round(QuarterPerMinuteValue, 2));
                }
                else
                    MPTK_TickLast = 0;

                return midievents;
            }
            catch (System.Exception ex)
            {
                MidiPlayerGlobal.ErrorDetail(ex);
            }
            return null;
        }

        /// <summary>
        /// Change speed to play. 1=normal speed
        /// </summary>
        /// <param name="speed"></param>
        public void ChangeSpeed(float speed)
        {
            try
            {
                //Debug.Log("ChangeSpeed " + speed);
                Speed = speed;
                if (QuarterPerMinuteValue > 0d)
                {
                    ChangeTempo(QuarterPerMinuteValue);
                    //CancelNextReadEvents = true;
                }
            }
            catch (System.Exception ex)
            {
                MidiPlayerGlobal.ErrorDetail(ex);
            }
        }

        public void ChangeQuantization(int level = 4)
        {
            try
            {
                if (level <= 0)
                    Quantization = 0;
                else
                    Quantization = midifile.DeltaTicksPerQuarterNote / level;
            }
            catch (System.Exception ex)
            {
                MidiPlayerGlobal.ErrorDetail(ex);
            }
        }

        /// <summary>
        /// Calculate PulseLenghtMS from QuarterPerMinute value
        /// </summary>
        /// <param name="tempo"></param>
        public void ChangeTempo(double tempo)
        {
            try
            {
                QuarterPerMinuteValue = tempo;
                TickLengthMs = (1000d / ((QuarterPerMinuteValue * midifile.DeltaTicksPerQuarterNote) / 60f)) / Speed;
                //The BPM measures how many quarter notes happen in a minute. To work out the length of each pulse we can use the following formula: Pulse Length = 60 / (BPM * PPQN)
                //16  Sixteen Double croche

                // Update total time of midi play
                CalculateDuration();

                if (LogEvents)
                {
                    Debug.Log(string.Format("ChangeTempo QuarterPerMinuteValue:{0:0.00} Speed:{1:0.00} DeltaTicksPerQuarterNote:{2:0.00} PulseLength:{3:0.00} ms  Duration:{4:0.00} s",
                        QuarterPerMinuteValue, Speed, midifile.DeltaTicksPerQuarterNote, TickLengthMs, MPTK_Duration.TotalSeconds));
                }

            }
            catch (System.Exception ex)
            {
                MidiPlayerGlobal.ErrorDetail(ex);
            }
        }

        private void CalculateDuration()
        {
            if (MidiSorted != null && MidiSorted.Count > 0)
            {
                MPTK_Duration = TimeSpan.FromMilliseconds(MidiSorted[MidiSorted.Count - 1].Event.AbsoluteTime * TickLengthMs);
            }
        }

        public int CalculateNextPosEvents(double timeFromStartMS)
        {
            if (MidiSorted != null)
            {
                CurrentTick = MPTK_ConvertTimeToTick(timeFromStartMS);
                //Debug.Log(">>> CalculateNextPosEvents - CurrentPulse:" + CurrentTick + " CurrentNextPosEvent:" + NextPosEvent + " LastTimeFromStartMS:" + LastTimeFromStartMS + " timeFromStartMS:" + Convert.ToInt32(timeFromStartMS));
                if (CurrentTick == 0)
                {
                    NextPosEvent = 0;
                    LastTimeFromStartMS = 0;
                }
                else
                {
                    LastTimeFromStartMS = timeFromStartMS;
                    for (int currentPosEvent = 0; currentPosEvent < MidiSorted.Count; currentPosEvent++)
                    {
                        TrackMidiEvent trackEvent = MidiSorted[currentPosEvent];
                        //Debug.Log("CurrentPulse:" + CurrentPulse + " trackEvent:" + trackEvent.AbsoluteQuantize);

                        if (trackEvent.Event.AbsoluteTime > CurrentTick)// && CurrentPulse < nexttrackEvent.Event.AbsoluteTime )
                        {
                            NextPosEvent = currentPosEvent;
                            //Debug.Log("     CalculateNextPosEvents - NextPosEvent:" + NextPosEvent + " trackEvent:" + trackEvent.Event.AbsoluteTime + " timeFromStartMS:" + Convert.ToInt32(timeFromStartMS));
                            //Debug.Log("NextPosEvent:" + NextPosEvent);
                            break;
                        }
                        //if (currentPosEvent == MidiSorted.Count - 1) Debug.Log("Last CalculateNextPosEvents - currentPosEvent:" + currentPosEvent + " trackEvent:" + trackEvent.Event.AbsoluteTime + " timeFromStartMS:" + Convert.ToInt32(timeFromStartMS));
                    }
                }
                //Debug.Log("<<< CalculateNextPosEvents NextPosEvent:" + NextPosEvent);
            }
            return NextPosEvent;
        }

        /// <summary>
        /// Read a list of midi events available for the current time
        /// </summary>
        /// <param name="timeFromStartMS"></param>
        /// <returns></returns>
        public List<MPTKEvent> ReadMidiEvents(double timeFromStartMS)
        {
            List<MPTKEvent> midievents = null;
            try
            {
                EndMidiEvent = false;
                if (midifile != null)
                {
                    if (NextPosEvent < MidiSorted.Count)
                    {
                        // The BPM measures how many quarter notes happen in a minute. To work out the length of each pulse we can use the following formula: 
                        // Pulse Length = 60 / (BPM * PPQN)
                        // Calculate current pulse to play
                        CurrentTick += Convert.ToInt64((timeFromStartMS - LastTimeFromStartMS) / TickLengthMs);

                        LastTimeFromStartMS = timeFromStartMS;
                        // From the last position played
                        for (int currentPosEvent = NextPosEvent; currentPosEvent < MidiSorted.Count; currentPosEvent++)
                        {
                            TrackMidiEvent trackEvent = MidiSorted[currentPosEvent];
                            if (Quantization != 0)
                                trackEvent.AbsoluteQuantize = ((trackEvent.Event.AbsoluteTime + Quantization / 2) / Quantization) * Quantization;
                            else
                                trackEvent.AbsoluteQuantize = trackEvent.Event.AbsoluteTime;

                            //Debug.Log("ReadMidiEvents - timeFromStartMS:" + Convert.ToInt32(timeFromStartMS) + " LastTimeFromStartMS:" + Convert.ToInt32(LastTimeFromStartMS) + " CurrentPulse:" + CurrentPulse + " AbsoluteQuantize:" + trackEvent.AbsoluteQuantize);

                            if (trackEvent.AbsoluteQuantize <= CurrentTick)
                            {
                                NextPosEvent = currentPosEvent + 1;
                                if (midievents == null) midievents = new List<MPTKEvent>();
                                if (ConvertToEvent(midievents, trackEvent))
                                    break;
                            }
                            else
                                // Out of time, exit for loop
                                break;
                        }
                    }
                    else
                    {
                        // End of midi events
                        EndMidiEvent = true;
                    }
                }
            }
            catch (System.Exception ex)
            {
                MidiPlayerGlobal.ErrorDetail(ex);
            }

            if (midievents != null && midievents.Count > 0)
            {
                MPTK_TickCurrent = midievents.Last().Tick;
            }
            return midievents;
        }

        /// <summary>
        /// Add a TrackMidiEvent to a list of MPTKEvent.
        /// </summary>
        /// <param name="mptkEvents">Must be alloc before the call</param>
        /// <param name="trackEvent"></param>
        /// <returns></returns>
        private bool ConvertToEvent(List<MPTKEvent> mptkEvents, TrackMidiEvent trackEvent)
        {
            bool exitLoop = false;
            MPTKEvent midievent = null;
            switch (trackEvent.Event.CommandCode)
            {
                case MidiCommandCode.NoteOn:

                    if (((NoteOnEvent)trackEvent.Event).OffEvent != null)
                    {

                        NoteOnEvent noteon = (NoteOnEvent)trackEvent.Event;
                        //Debug.Log(string.Format("Track:{0} NoteNumber:{1,3:000} AbsoluteTime:{2,6:000000} NoteLength:{3,6:000000} OffDeltaTime:{4,6:000000} ", track, noteon.NoteNumber, noteon.AbsoluteTime, noteon.NoteLength, noteon.OffEvent.DeltaTime));
                        if (noteon.NoteLength > 0)
                        {
                            midievent = new MPTKEvent()
                            {
                                Track = trackEvent.IndexTrack,
                                Tick = trackEvent.AbsoluteQuantize,
                                Command = MPTKCommand.NoteOn,
                                Value = noteon.NoteNumber,
                                Channel = trackEvent.Event.Channel - 1,
                                Velocity = noteon.Velocity,
                                Duration = Convert.ToInt64(noteon.NoteLength * TickLengthMs),
                                Length = noteon.NoteLength,
                            };
                            mptkEvents.Add(midievent);
                            if (LogEvents)
                            {
                                string notename = (midievent.Channel != 9) ?
                                    String.Format("{0}{1}", NoteNames[midievent.Value % 12], midievent.Value / 12) : "Drum";
                                Debug.Log(BuildInfoTrack(trackEvent) + string.Format("NoteOn  {0,3:000}\t{1,-4}\tLenght:{2,5}\t{3}\tVeloc:{4,3}",
                                    midievent.Value, notename, noteon.NoteLength, NoteLength(midievent), noteon.Velocity));
                            }
                        }
                        else if (KeepNoteOff)
                            midievent = CreateNoteOff(mptkEvents, trackEvent);
                    }
                    break;

                case MidiCommandCode.NoteOff:
                    midievent = CreateNoteOff(mptkEvents, trackEvent);
                    break;

                case MidiCommandCode.ControlChange:

                    ControlChangeEvent controlchange = (ControlChangeEvent)trackEvent.Event;
                    midievent = new MPTKEvent()
                    {
                        Track = trackEvent.IndexTrack,
                        Tick = trackEvent.AbsoluteQuantize,
                        Command = MPTKCommand.ControlChange,
                        Channel = trackEvent.Event.Channel - 1,
                        Controller = (MPTKController)controlchange.Controller,
                        Value = controlchange.ControllerValue,

                    };
                    //if ((MPTKController)controlchange.Controller != MPTKController.Sustain)
                    mptkEvents.Add(midievent);

                    // Other midi event
                    if (LogEvents)
                        Debug.Log(BuildInfoTrack(trackEvent) + string.Format("Control {0} {1}", controlchange.Controller, controlchange.ControllerValue));

                    break;

                case MidiCommandCode.PatchChange:
                    PatchChangeEvent change = (PatchChangeEvent)trackEvent.Event;
                    midievent = new MPTKEvent()
                    {
                        Track = trackEvent.IndexTrack,
                        Tick = trackEvent.AbsoluteQuantize,
                        Command = MPTKCommand.PatchChange,
                        Channel = trackEvent.Event.Channel - 1,
                        Value = change.Patch,
                    };
                    mptkEvents.Add(midievent);
                    if (LogEvents)
                        Debug.Log(BuildInfoTrack(trackEvent) + string.Format("Patch   {0,3:000} {1}", change.Patch, PatchChangeEvent.GetPatchName(change.Patch)));
                    break;

                case MidiCommandCode.MetaEvent:
                    MetaEvent meta = (MetaEvent)trackEvent.Event;
                    midievent = new MPTKEvent()
                    {
                        Track = trackEvent.IndexTrack,
                        Tick = trackEvent.AbsoluteQuantize,
                        Command = MPTKCommand.MetaEvent,
                        Channel = trackEvent.Event.Channel - 1,
                        Meta = (MPTKMeta)meta.MetaEventType,
                    };

                    switch (meta.MetaEventType)
                    {
                        case MetaEventType.EndTrack:
                            midievent.Info = "End Track";
                            break;

                        case MetaEventType.TimeSignature:
                            AnalyzeTimeSignature(meta);
                            break;

                        case MetaEventType.SetTempo:
                            TempoEvent tempo = (TempoEvent)meta;
                            // Tempo change will be done in MidiFilePlayer
                            midievent.Duration = (long)tempo.Tempo;
                            MPTK_MicrosecondsPerQuarterNote = tempo.MicrosecondsPerQuarterNote;
                            // Force exit loop
                            exitLoop = true;
                            break;

                        case MetaEventType.SequenceTrackName:
                            midievent.Info = ((TextEvent)meta).Text;
                            if (!string.IsNullOrEmpty(SequenceTrackName)) SequenceTrackName += "\n";
                            SequenceTrackName += string.Format("T{0,2:00} {1}", trackEvent.IndexTrack, midievent.Info);
                            break;

                        case MetaEventType.ProgramName:
                            midievent.Info = ((TextEvent)meta).Text;
                            ProgramName += midievent.Info + " ";
                            break;

                        case MetaEventType.TrackInstrumentName:
                            midievent.Info = ((TextEvent)meta).Text;
                            if (!string.IsNullOrEmpty(TrackInstrumentName)) TrackInstrumentName += "\n";
                            TrackInstrumentName += string.Format("T{0,2:00} {1}", trackEvent.IndexTrack, midievent.Info);
                            break;

                        case MetaEventType.TextEvent:
                            midievent.Info = ((TextEvent)meta).Text;
                            TextEvent += midievent.Info + " ";
                            break;

                        case MetaEventType.Copyright:
                            midievent.Info = ((TextEvent)meta).Text;
                            Copyright += midievent.Info + " ";
                            break;

                        case MetaEventType.Lyric: // lyric
                            midievent.Info = ((TextEvent)meta).Text;
                            TextEvent += midievent.Info + " ";
                            break;

                        case MetaEventType.Marker: // marker
                            midievent.Info = ((TextEvent)meta).Text;
                            TextEvent += midievent.Info + " ";
                            break;

                        case MetaEventType.CuePoint: // cue point
                        case MetaEventType.DeviceName:
                            break;
                    }

                    if (LogEvents && !string.IsNullOrEmpty(midievent.Info))
                        Debug.Log(BuildInfoTrack(trackEvent) + string.Format("Meta     {0,-15} '{1}'", midievent.Meta, midievent.Info));

                    mptkEvents.Add(midievent);
                    //Debug.Log(BuildInfoTrack(trackEvent) + string.Format("Meta {0} {1}", meta.MetaEventType, meta.ToString()));
                    break;

                default:
                    // Other midi event
                    if (LogEvents)
                        Debug.Log(BuildInfoTrack(trackEvent) + string.Format("Other    {0,-15} Not handle by MPTK", trackEvent.Event.CommandCode));
                    break;
            }

            return exitLoop;
        }

        private MPTKEvent CreateNoteOff(List<MPTKEvent> mptkEvents, TrackMidiEvent trackEvent)
        {
            MPTKEvent midievent;
            NoteEvent noteoff = (NoteEvent)trackEvent.Event;
            //Debug.Log(string.Format("Track:{0} NoteNumber:{1,3:000} AbsoluteTime:{2,6:000000} NoteLength:{3,6:000000} OffDeltaTime:{4,6:000000} ", track, noteon.NoteNumber, noteon.AbsoluteTime, noteon.NoteLength, noteon.OffEvent.DeltaTime));
            midievent = new MPTKEvent()
            {
                Track = trackEvent.IndexTrack,
                Tick = trackEvent.AbsoluteQuantize,
                Command = MPTKCommand.NoteOff,
                Value = noteoff.NoteNumber,
                Channel = trackEvent.Event.Channel - 1,
                Velocity = noteoff.Velocity,
                Duration = 0,
                Length = 0,
            };
            mptkEvents.Add(midievent);
            if (LogEvents)
            {
                string notename = (midievent.Channel != 9) ?
                    String.Format("{0}{1}", NoteNames[midievent.Value % 12], midievent.Value / 12) : "Drum";
                Debug.Log(BuildInfoTrack(trackEvent) + string.Format("NoteOff {0,3:000}\t{1,-4}\tLenght:{2}", midievent.Value, notename, " Note Off"));
            }

            return midievent;
        }

        /// <summary>
        /// https://en.wikipedia.org/wiki/Note_value
        /// </summary>
        /// <param name="note"></param>
        /// <returns></returns>
        public MPTKEvent.EnumLength NoteLength(MPTKEvent note)
        {
            if (midifile != null)
            {
                if (note.Length >= midifile.DeltaTicksPerQuarterNote * 4)
                    return MPTKEvent.EnumLength.Whole;
                else if (note.Length >= midifile.DeltaTicksPerQuarterNote * 2)
                    return MPTKEvent.EnumLength.Half;
                else if (note.Length >= midifile.DeltaTicksPerQuarterNote)
                    return MPTKEvent.EnumLength.Quarter;
                else if (note.Length >= midifile.DeltaTicksPerQuarterNote / 2)
                    return MPTKEvent.EnumLength.Eighth;
            }
            return MPTKEvent.EnumLength.Sixteenth;
        }

        private void AnalyzeTimeSignature(MetaEvent meta)
        {
            TimeSignatureEvent timesig = (TimeSignatureEvent)meta;
            // Numerator: counts the number of beats in a measure. 
            // For example a numerator of 4 means that each bar contains four beats. 
            MPTK_TimeSigNumerator = timesig.Numerator;
            // Denominator: number of quarter notes in a beat.0=ronde, 1=blanche, 2=quarter, 3=eighth, etc. 
            MPTK_TimeSigDenominator = timesig.Denominator;
            MPTK_NumberBeatsMeasure = timesig.Numerator;
            MPTK_NumberQuarterBeat = System.Convert.ToInt32(Mathf.Pow(2f, timesig.Denominator));
            MPTK_TicksInMetronomeClick = timesig.TicksInMetronomeClick;
            MPTK_No32ndNotesInQuarterNote = timesig.No32ndNotesInQuarterNote;
        }

        /// <summary>
        /// Read midi event as Control, Tempo and Patch change from start to position
        /// </summary>
        /// <param name="position"></param>
        /// <returns></returns>
        public List<MPTKEvent> ReadChangeFromStart(int position)
        {
            List<MPTKEvent> midievents = new List<MPTKEvent>(); ;
            try
            {
                if (midifile != null)
                {
                    if (position < 0 || position >= MidiSorted.Count)
                        position = MidiSorted.Count - 1;

                    for (int currentPosEvent = 0; currentPosEvent < position; currentPosEvent++)
                    {
                        TrackMidiEvent trackEvent = MidiSorted[currentPosEvent];
                        MPTKEvent midievent = null;
                        switch (trackEvent.Event.CommandCode)
                        {
                            case MidiCommandCode.ControlChange:
                                ControlChangeEvent controlchange = (ControlChangeEvent)trackEvent.Event;
                                midievent = new MPTKEvent()
                                {
                                    Tick = trackEvent.AbsoluteQuantize,
                                    Command = MPTKCommand.ControlChange,
                                    Channel = trackEvent.Event.Channel - 1,
                                    Controller = (MPTKController)controlchange.Controller,
                                    Value = controlchange.ControllerValue,

                                };
                                break;
                            case MidiCommandCode.PatchChange:
                                PatchChangeEvent change = (PatchChangeEvent)trackEvent.Event;
                                midievent = new MPTKEvent()
                                {
                                    Tick = trackEvent.AbsoluteQuantize,
                                    Command = MPTKCommand.PatchChange,
                                    Channel = trackEvent.Event.Channel - 1,
                                    Value = change.Patch,
                                };
                                break;

                            case MidiCommandCode.MetaEvent:
                                MetaEvent meta = (MetaEvent)trackEvent.Event;
                                if (meta.MetaEventType == MetaEventType.SetTempo)
                                {
                                    TempoEvent tempo = (TempoEvent)meta;
                                    midievent = new MPTKEvent()
                                    {
                                        Tick = trackEvent.AbsoluteQuantize,
                                        Command = MPTKCommand.MetaEvent,
                                        Channel = trackEvent.Event.Channel - 1,
                                        Meta = (MPTKMeta)meta.MetaEventType,
                                        Duration = (long)tempo.Tempo,
                                    };
                                }
                                break;
                        }
                        if (midievent != null)
                            midievents.Add(midievent);
                    }

                }
            }
            catch (System.Exception ex)
            {
                MidiPlayerGlobal.ErrorDetail(ex);
            }
            return midievents;
        }

        private string BuildInfoTrack(TrackMidiEvent e)
        {
            return string.Format("[A:{0,5:00000} Q:{1,5:00000} P:{2,5:00000}] [T:{3,2:00} C:{4,2:00}] ", e.Event.AbsoluteTime, e.AbsoluteQuantize, CurrentTick, e.IndexTrack, e.Event.Channel);
        }

        public void DebugTrack()
        {
            int itrck = 0;
            foreach (IList<MidiEvent> track in midifile.Events)
            {
                itrck++;
                foreach (MidiEvent midievent in track)
                {
                    string info = string.Format("Track:{0} Channel:{1,2:00} Command:{2} AbsoluteTime:{3:0000000} ", itrck, midievent.Channel, midievent.CommandCode, midievent.AbsoluteTime);
                    if (midievent.CommandCode == MidiCommandCode.NoteOn)
                    {
                        NoteOnEvent noteon = (NoteOnEvent)midievent;
                        if (noteon.OffEvent == null)
                            info += string.Format(" OffEvent null");
                        else
                            info += string.Format(" OffEvent.DeltaTimeChannel:{0:0000.00} ", noteon.OffEvent.DeltaTime);
                    }
                    Debug.Log(info);
                }
            }
        }
        public void DebugMidiSorted()
        {
            foreach (TrackMidiEvent midievent in MidiSorted)
            {
                string info = string.Format("Track:{0} Channel:{1,2:00} Command:{2} AbsoluteTime:{3:0000000} ", midievent.IndexTrack, midievent.Event.Channel, midievent.Event.CommandCode, midievent.Event.AbsoluteTime);
                switch (midievent.Event.CommandCode)
                {
                    case MidiCommandCode.NoteOn:
                        NoteOnEvent noteon = (NoteOnEvent)midievent.Event;
                        if (noteon.Velocity == 0)
                            info += string.Format(" Velocity 0");
                        if (noteon.OffEvent == null)
                            info += string.Format(" OffEvent null");
                        else
                            info += string.Format(" OffEvent.DeltaTimeChannel:{0:0000.00} ", noteon.OffEvent.DeltaTime);
                        break;
                }
                Debug.Log(info);
            }
        }
        //! @endcond
    }
}

