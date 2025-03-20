using NAudio.Midi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Windows.Devices.Enumeration;
using Windows.Devices.Midi;

namespace MidiEdit
{
    class Program
    {
        static int resolution;
        static IMidiOutPort outPort;

        /// <summary>
        /// アプリケーションのメイン エントリ ポイントです。
        /// </summary>
        [STAThread]
        static void Main()
        {
            //MIDI出力ポートを最初に確保しておく
            //メインスレッドからじゃないと確保できないっぽい（？）
            Task<IMidiOutPort> port = PrepareMidiOutPort();
            port.Wait();


            string filename = "";
            //filename = @"C:\\Users\\WOOO\\Desktop\\一時\\TEST.mid";

            MidiEdit.Form1.midi_filename = filename;
           
            //Task task = PlayMidi(filename);
            //task.Wait();


            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());
        }

        static public void Play(string filename)
        {
            Task task = PlayMidi(filename);
            //task.Wait();
        }

        static public void MidiInfoSet(string filename)
        {
            Task task = GetMidiInfo(filename);
            //task.Wait();
        }

        static public void MidiInfoSetPattern(string filename)
        {
            Task task = GetMidiInfoPattern(filename);
            //task.Wait();
        }

        // MIDI ファイルを再生する
        private static async Task PlayMidi(string filename)
        {
            MidiFile midiFile = new MidiFile(filename);

            // 行儀は悪いが、Sequencer_OnMidiEvent から使う変数をここでフィールドに代入する
            Program.resolution = midiFile.DeltaTicksPerQuarterNote;

            // シーケンサーを作成する
            MidiSequencer sequencer = new MidiSequencer(outPort, midiFile.Events);
            sequencer.OnMidiEvent += Sequencer_OnMidiEvent;

            // 実際に再生する
            await sequencer.Play();
        }

        // MIDI ファイルの情報を取得
        private static async Task GetMidiInfo(string filename)
        {
            MidiFile midiFile = new MidiFile(filename);

            // 行儀は悪いが、Sequencer_OnMidiEvent から使う変数をここでフィールドに代入する
            Program.resolution = midiFile.DeltaTicksPerQuarterNote;

            // シーケンサーを作成する
            MidiSequencer sequencer = new MidiSequencer(outPort, midiFile.Events);
            sequencer.OnMidiEvent += Sequencer_OnMidiEvent;

            // 実際に再生する
            await sequencer.GetMidiInfo();
        }

        // MIDI ファイルの情報を取得
        private static async Task GetMidiInfoPattern(string filename)
        {
            MidiFile midiFile = new MidiFile(filename);

            // 行儀は悪いが、Sequencer_OnMidiEvent から使う変数をここでフィールドに代入する
            Program.resolution = midiFile.DeltaTicksPerQuarterNote;

            // シーケンサーを作成する
            MidiSequencer sequencer = new MidiSequencer(outPort, midiFile.Events);
            sequencer.OnMidiEvent += Sequencer_OnMidiEvent;

            // 実際に再生する
            await sequencer.GetMidiInfoPattern();
        }

        // MIDI 出力ポートを取得する
        private static async Task<IMidiOutPort> PrepareMidiOutPort()
        {
            string selector = MidiOutPort.GetDeviceSelector();
            DeviceInformationCollection deviceInformationCollection = await DeviceInformation.FindAllAsync(selector);
            if (deviceInformationCollection?.Count > 0)
            {
                // collection has items
                string id = deviceInformationCollection[0].Id;
                outPort = await MidiOutPort.FromIdAsync(id);
                return outPort;
            }
            else
            {
                // collection is null or empty
                throw new InvalidOperationException($"No MIDI device for {selector}");
            }
        }

        // MIDI イベントの処理ハンドラー
        private static void Sequencer_OnMidiEvent(MidiEvent e)
        {
            // 今回は 4/4 拍子であることが分かっているので 4 で割っているが、
            // MIDI ファイル内には拍子情報が必須ではないため実際に任意の曲で小節数を出そうとすると難しい.
            // ちなみに、拍子情報がある場合は MetaEvent として現れる.
            long measure = (e.AbsoluteTime / Program.resolution / 4) + 1;

            // 次の内容で出力する: [小節数 : 経過時間(Ticks) : イベントの内容]
            Console.WriteLine($"{measure,3} : {e.AbsoluteTime,5} : {e}");
        }



    }
}
