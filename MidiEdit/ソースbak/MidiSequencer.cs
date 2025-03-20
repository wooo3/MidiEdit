using NAudio.Midi;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Devices.Midi;
using System.Windows.Forms;


// MIDI イベント発生時の処理
public delegate void MidiEventHandler(MidiEvent e);

public class MidiSequencer
{
    // MIDI 出力ポート
    private readonly IMidiOutPort outPort;
    // MIDI データ
    private readonly MidiEventCollection midiEvents;

    // MIDI イベント発生時の処理ハンドラー
    public event MidiEventHandler OnMidiEvent;

    // 再生したMIDIのノート情報を格納する
    const int maxNote = 1024;
    static public int[] playNoteArray = new int[maxNote];
    static public int[] playLengthArray = new int[maxNote];

    static public int[] playNoteArrayPattern = new int[maxNote];
    static public int[] playLengthArrayPattern = new int[maxNote];


    public MidiSequencer(IMidiOutPort outPort, MidiEventCollection midiEvents)
    {
        this.outPort = outPort ?? throw new ArgumentNullException(nameof(outPort));
        this.midiEvents = midiEvents ?? throw new ArgumentNullException(nameof(midiEvents));
    }

    public async Task Play()
    {
        TempoData tempo = new TempoData(midiEvents);

        // 完了したトラック数
        int finishedTracks = 0;
        // 各トラックの再生済みイベント数
        int[] eventIndices = new int[midiEvents.Tracks];

        // イベントがひとつもないトラックは始まる前から終わってる.
        for (int i = 0; i < midiEvents.Tracks; ++i)
        {
            IList<MidiEvent> currentTrack = midiEvents[i];
            if (currentTrack.Count == 0) ++finishedTracks;
        }

        // ここから曲の再生を開始する
        Stopwatch stopWatch = new Stopwatch();
        stopWatch.Start();

        while (finishedTracks != midiEvents.Tracks)
        {
            // 経過時間 (microseconds)
            long elapsed = stopWatch.ElapsedMilliseconds * 1000L;
            // 経過時間 (Ticks)
            long elapsedTicks = tempo.MicrosecondsToTicks(elapsed);

            //  elapsedTicks が負の場合はバグか何か
            if (elapsedTicks < 0) throw new InvalidProgramException($"elapsedTicks = {elapsedTicks} < 0 !!");

            int addCount = 0;

            for (int i = 0; i < midiEvents.Tracks; ++i)
            {
                IList<MidiEvent> currentTrack = midiEvents[i];

                //ノートの情報を取得
                {
                    //配列初期化
                    for (int j = 0; j < playNoteArray.Length; j++)
                    {
                        playNoteArray[j] = 0;
                        playLengthArray[j] = 0;
                    }
                    //NoteOnの情報を取得する
                    for (int j = 0; j < currentTrack.Count; j++)
                    {
                        MidiEvent currentEvent = currentTrack[j];
                        IMidiMessage messageToSend = ConvertToMidiMessageOrNull(currentEvent);
                        if (messageToSend != null)
                        {
                            if (messageToSend.Type == MidiMessageType.NoteOn)
                            {
                                NoteEvent @event = currentEvent as NoteEvent;
                                playNoteArray[addCount] = (byte)@event.NoteNumber;
                                playLengthArray[addCount] = GetMidiLength(currentEvent);
                                addCount++;
                            }
                        }
                    }
                }

                // このトラックについて再生が終了していれば次のトラックへ
                if (eventIndices[i] == currentTrack.Count) continue;

                while (currentTrack[eventIndices[i]].AbsoluteTime <= elapsedTicks)
                {
                    // 再生されるべき時刻(AbsoluteTime) を過ぎていれば再生する
                    MidiEvent currentEvent = currentTrack[eventIndices[i]];

                    // イベントの通知
                    OnMidiEvent?.Invoke(currentEvent);

                    // 出力ポートに送信するオブジェクトに変換する(送信できないイベントの場合 null が返される)
                    IMidiMessage messageToSend = ConvertToMidiMessageOrNull(currentEvent);
                    if (messageToSend != null)
                    {
                        outPort.SendMessage(messageToSend);

                        NoteEvent @event = currentEvent as NoteEvent;
                        int noteNum = (byte)@event.NoteNumber;

                        /*
                        if (messageToSend.Type == MidiMessageType.NoteOn)
                        {
                            //ピアノが押されたとき
                            MidiEdit.Form1.noteStatus[noteNum] = 1;
                        }
                        else if (messageToSend.Type == MidiMessageType.NoteOff)
                        {
                            //ピアノが離したとき
                            MidiEdit.Form1.noteStatus[noteNum] = 0;
                        }
                        */
                    }

                    // 消費済みイベント数をインクリメント
                    ++eventIndices[i];
                    if (eventIndices[i] == currentTrack.Count)
                    {
                        // トラック内の全イベントが消化されていれば完了トラック数をインクリメントし、このトラックについてのループを抜ける
                        ++finishedTracks;
                        break;
                    }
                }
            }
            // 1ms のディレイを入れる
            await Task.Delay(1);
        }
    }// #Play()

    static int software_type = 0;

    public async Task GetMidiInfo()
    {
        TempoData tempo = new TempoData(midiEvents);

        // 完了したトラック数
        int finishedTracks = 0;
        // 各トラックの再生済みイベント数
        int[] eventIndices = new int[midiEvents.Tracks];

        // イベントがひとつもないトラックは始まる前から終わってる.
        for (int i = 0; i < midiEvents.Tracks; ++i)
        {
            IList<MidiEvent> currentTrack = midiEvents[i];
            if (currentTrack.Count == 0) ++finishedTracks;
        }

        // ここから曲の再生を開始する
        Stopwatch stopWatch = new Stopwatch();
        stopWatch.Start();

        while (finishedTracks != midiEvents.Tracks)
        {
            // 経過時間 (microseconds)
            long elapsed = stopWatch.ElapsedMilliseconds * 1000L;
            // 経過時間 (Ticks)
            long elapsedTicks = tempo.MicrosecondsToTicks(elapsed);

            //  elapsedTicks が負の場合はバグか何か
            if (elapsedTicks < 0) throw new InvalidProgramException($"elapsedTicks = {elapsedTicks} < 0 !!");

            int addCount = 0;

            for (int i = 0; i < midiEvents.Tracks; ++i)
            {
                IList<MidiEvent> currentTrack = midiEvents[i];

                //ノートの情報を取得
                {
                    //配列初期化
                    for (int j = 0; j < playNoteArray.Length; j++)
                    {
                        playNoteArray[j] = 0;
                        playLengthArray[j] = 0;
                    }
                    //NoteOnの情報を取得する
                    bool isEnd = false; // 単一トラックのMIDIのみとする。なのでトラックにNoteonがあったら抜ける
                    for (int j = 0; j < currentTrack.Count; j++)
                    {
                        MidiEvent currentEvent = currentTrack[j];
                        IMidiMessage messageToSend = ConvertToMidiMessageOrNull(currentEvent);
                        if (messageToSend != null)
                        {
                            if (messageToSend.Type == MidiMessageType.NoteOn)
                            {
                                NoteEvent @event = currentEvent as NoteEvent;
                                playNoteArray[addCount] = (byte)@event.NoteNumber;
                                playLengthArray[addCount] = GetMidiLength(currentEvent);

                                //duration gateTime　はソフトによって違うらしい。。。
                                if (software_type == 1)
                                {
                                    playLengthArray[addCount] = GetMidiLength(currentEvent) / 1920 * 384;

                                    if(playLengthArray[addCount] > 384)
                                    {
                                        playLengthArray[addCount] = 384;
                                    }
                                }

                                addCount++;

                                isEnd = true;
                            }
                        }
                    }
                    if (isEnd)
                    {
                        return;
                    }
                }

                // このトラックについて再生が終了していれば次のトラックへ
                if (eventIndices[i] == currentTrack.Count) continue;
            }
            // 1ms のディレイを入れる
            await Task.Delay(1);
        }
    }// #Play()


    public async Task GetMidiInfoPattern()
    {
        TempoData tempo = new TempoData(midiEvents);

        // 完了したトラック数
        int finishedTracks = 0;
        // 各トラックの再生済みイベント数
        int[] eventIndices = new int[midiEvents.Tracks];

        // イベントがひとつもないトラックは始まる前から終わってる.
        for (int i = 0; i < midiEvents.Tracks; ++i)
        {
            IList<MidiEvent> currentTrack = midiEvents[i];
            if (currentTrack.Count == 0) ++finishedTracks;
        }

        // ここから曲の再生を開始する
        Stopwatch stopWatch = new Stopwatch();
        stopWatch.Start();

        while (finishedTracks != midiEvents.Tracks)
        {
            // 経過時間 (microseconds)
            long elapsed = stopWatch.ElapsedMilliseconds * 1000L;
            // 経過時間 (Ticks)
            long elapsedTicks = tempo.MicrosecondsToTicks(elapsed);

            //  elapsedTicks が負の場合はバグか何か
            if (elapsedTicks < 0) throw new InvalidProgramException($"elapsedTicks = {elapsedTicks} < 0 !!");

            int addCount = 0;

            for (int i = 0; i < midiEvents.Tracks; ++i)
            {
                IList<MidiEvent> currentTrack = midiEvents[i];

                //ノートの情報を取得
                {
                    //配列初期化
                    for (int j = 0; j < playNoteArrayPattern.Length; j++)
                    {
                        playNoteArrayPattern[j] = 0;
                        playLengthArrayPattern[j] = 0;
                    }
                    //NoteOnの情報を取得する
                    bool isEnd = false; // 単一トラックのMIDIのみとする。なのでトラックにNoteonがあったら抜ける
                    for (int j = 0; j < currentTrack.Count; j++)
                    {
                        MidiEvent currentEvent = currentTrack[j];
                        IMidiMessage messageToSend = ConvertToMidiMessageOrNull(currentEvent);
                        if (messageToSend != null)
                        {
                            if (messageToSend.Type == MidiMessageType.NoteOn)
                            {
                                NoteEvent @event = currentEvent as NoteEvent;
                                playNoteArrayPattern[addCount] = (byte)@event.NoteNumber;
                                playLengthArrayPattern[addCount] = GetMidiLength(currentEvent);

                                //duration gateTime　はソフトによって違うらしい。。。
                                if (software_type == 1)
                                {
                                    playLengthArrayPattern[addCount] = GetMidiLength(currentEvent) / 1920 * 384;

                                    if (playLengthArrayPattern[addCount] > 384)
                                    {
                                        playLengthArrayPattern[addCount] = 384;
                                    }
                                }

                                addCount++;

                                isEnd = true;
                            }
                        }
                    }
                    if (isEnd)
                    {
                        return;
                    }
                }

                // このトラックについて再生が終了していれば次のトラックへ
                if (eventIndices[i] == currentTrack.Count) continue;
            }
            // 1ms のディレイを入れる
            await Task.Delay(1);
        }
    }// #Play()


    /// <summary>
    /// NAudio が定義したMIDIイベントオブジェクトを、Microsoft が定義したものに変換する.
    /// </summary>
    /// <param name="midiEvent">NAudio が定義したMIDIイベントオブジェクト</param>
    /// <returns>対応するクラスが存在する場合 Microsoft が定義したメッセージ. さもなくば null</returns>
    private IMidiMessage ConvertToMidiMessageOrNull(MidiEvent midiEvent)
    {
        switch (midiEvent.CommandCode)
        {
            case MidiCommandCode.NoteOff:
                {// 8n
                    NoteEvent @event = midiEvent as NoteEvent;
                    return new MidiNoteOffMessage((byte)@event.Channel, (byte)@event.NoteNumber, (byte)@event.Velocity);
                }
            case MidiCommandCode.NoteOn:
                {// 9n
                    NoteOnEvent @event = midiEvent as NoteOnEvent;
                    return new MidiNoteOnMessage((byte)@event.Channel, (byte)@event.NoteNumber, (byte)@event.Velocity);
                }
            case MidiCommandCode.KeyAfterTouch:
                {// An
                    NoteEvent @event = midiEvent as NoteEvent;
                    return new MidiPolyphonicKeyPressureMessage((byte)@event.Channel, (byte)@event.NoteNumber, (byte)@event.Velocity);
                }
            case MidiCommandCode.ControlChange:
                {// Bn
                    ControlChangeEvent @event = midiEvent as ControlChangeEvent;
                    return new MidiControlChangeMessage((byte)@event.Channel, (byte)@event.Controller, (byte)@event.ControllerValue);
                }
            case MidiCommandCode.PatchChange:
                {// Cn
                    PatchChangeEvent @event = midiEvent as PatchChangeEvent;
                    return new MidiProgramChangeMessage((byte)@event.Channel, (byte)@event.Patch);
                }
            case MidiCommandCode.ChannelAfterTouch:
                {// Dn
                    ChannelAfterTouchEvent @event = midiEvent as ChannelAfterTouchEvent;
                    return new MidiChannelPressureMessage((byte)@event.Channel, (byte)@event.AfterTouchPressure);
                }
            case MidiCommandCode.PitchWheelChange:
                {// En
                    PitchWheelChangeEvent @event = midiEvent as PitchWheelChangeEvent;
                    return new MidiPitchBendChangeMessage((byte)@event.Channel, (byte)@event.Pitch);
                }
            case MidiCommandCode.Sysex:
                {// F0
                    SysexEvent @event = midiEvent as SysexEvent;
                    MemoryStream ms = new MemoryStream();
                    BinaryWriter bw = new BinaryWriter(ms);
                    long _ = 0L;
                    @event.Export(ref _, bw);
                    bw.Flush();
                    return new MidiSystemExclusiveMessage(ms.ToArray().AsBuffer());
                }
            case MidiCommandCode.Eox: // F7
            case MidiCommandCode.TimingClock: // F8
            case MidiCommandCode.StartSequence: // FA
            case MidiCommandCode.ContinueSequence: // FB
            case MidiCommandCode.StopSequence: // FC
            case MidiCommandCode.AutoSensing: // FE
            case MidiCommandCode.MetaEvent: // FF
                                            // これらは対応する IMidiMessage が存在しない
                return null;
            default: throw new InvalidOperationException($"Unknown MidiCommandCode: {midiEvent.CommandCode}");
        }
    }// #ConvertToMidiMessageOrNull(MidiEvent)

    private int GetMidiLength(MidiEvent midiEvent)
    {
        switch (midiEvent.CommandCode)
        {
            case MidiCommandCode.NoteOn:
                {// 9n
                    NoteOnEvent @event = midiEvent as NoteOnEvent;
                    if(@event.NoteLength == null)
                    {
                        return 0;   //エラーっぽい
                    }
                    return @event.NoteLength;
                    break;
                }
        }
        return 0;
    }
}// class MidiSequencer