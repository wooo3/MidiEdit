using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Runtime.InteropServices;

using NAudio.Midi;
using Windows.Devices.Enumeration;
using Windows.Devices.Midi;
using System.IO;

namespace MidiEdit
{
    public partial class Form1 : Form
    {
        static public string midi_filename;
        static public string midi_filename_pattern;
        static public bool isFixChordMidi = false;
        static public bool isChordNoteOnly = false;


        const int NOTE_MAX = 128;
        static public int[] noteStatus = new int[NOTE_MAX];
        static public int[] noteStatusBefore = new int[NOTE_MAX];
        Control[] cs = new Control[NOTE_MAX];

        List<int> noteList = new List<int> { };     //一度でも出てきた音
        List<int> noteList_highPrio = new List<int> { };     //３回以上出てきた音
        List<int> noteList_Chord = new List<int> { };        //和音生成中に出てきた音
        static int highPrio_border = 3;

        static public int songKey = 0;
        static public int songKeyBefore = 0;
        Control[] songKeyCs = new Control[12];
        string songKeyLabelText;

        Image currentImage;

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern bool AllocConsole();




        public Form1()
        {
            //画像ファイルを読み込む
            string path = System.Windows.Forms.Application.StartupPath;
            path += "\\hisa3.png";
            currentImage = Image.FromFile(path);

            InitializeComponent();

            for (int i = 36; i < NOTE_MAX; i++)
            {
                string controlName = "button" + i.ToString();
                //buttonをさがす。子コントロールは検索しない。
                Control buttonN = this.Controls[controlName];
                //buttonが見つかれば、Textを変更する
                if (buttonN != null)
                {
                    cs[i] = buttonN;
                }
            }

            for (int i = 0; i < 12; i++)
            {
                string controlName = "key" + (i + 1).ToString();
                //buttonをさがす。子コントロールは検索しない。
                Control buttonN = this.Controls[controlName];
                //buttonが見つかれば、Textを変更する
                if (buttonN != null)
                {
                    songKeyCs[i] = buttonN;
                    //KeyLabel.Text = buttonN.Text;
                }
            }

            //AllocConsole();
            backgroundWorker1.RunWorkerAsync();
        }


        private void LoadMidiFile_Click(object sender, EventArgs e)
        {
            string midiFileName = "";

            //OpenFileDialogクラスのインスタンスを作成
            OpenFileDialog ofd = new OpenFileDialog();

            //はじめのファイル名を指定する
            //はじめに「ファイル名」で表示される文字列を指定する
            ofd.FileName = "default.midi";
            //はじめに表示されるフォルダを指定する
            //指定しない（空の文字列）の時は、現在のディレクトリが表示される
            ofd.InitialDirectory = @"C:\";
            //[ファイルの種類]に表示される選択肢を指定する
            //指定しないとすべてのファイルが表示される
            ofd.Filter = "midiファイル(*.midi;*.mid)|*.midi;*.mid|すべてのファイル(*.*)|*.*";
            //[ファイルの種類]ではじめに選択されるものを指定する
            ofd.FilterIndex = 1;
            //タイトルを設定する
            ofd.Title = "開くファイルを選択してください";
            //ダイアログボックスを閉じる前に現在のディレクトリを復元するようにする
            ofd.RestoreDirectory = true;
            //存在しないファイルの名前が指定されたとき警告を表示する
            //デフォルトでTrueなので指定する必要はない
            ofd.CheckFileExists = true;
            //存在しないパスが指定されたとき警告を表示する
            //デフォルトでTrueなので指定する必要はない
            ofd.CheckPathExists = true;

            //ダイアログを表示する
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                //OKボタンがクリックされたとき、選択されたファイル名を表示する
                Console.WriteLine(ofd.FileName);
                midiFileName = ofd.FileName;

                midi_filename = midiFileName;
                LoadMidiName.Text = midiFileName;
                DebugWindows.Text = midiFileName + " 読み込み\n" + DebugWindows.Text;
            }
        }


        // ピアノの色変更
        private void PianoColorChange()
        {
            for (int i=36; i<NOTE_MAX; i++)
            {
                if (cs[i] != null)
                {
                    if (noteStatus[i] == 1)
                    {
                        // 背景色を変更します
                        cs[i].BackColor = Color.BurlyWood;
                    }
                    else
                    {
                        //離したので元の色に戻す
                        cs[i].BackColor = cs[i].ForeColor;
                    }
                }
            }
        }
        private void KeyIdentification()
        {
            //FLのC4が48
            int[,] chord = new int[12, 7] {
                { 48, 50, 52, 53, 55, 57, 59 } ,
                { 48, 50, 52, 54, 55, 57, 59 } ,
                { 49, 50, 52, 54, 55, 57, 59 } ,
                { 49, 50, 52, 54, 56, 57, 59 } ,
                { 49, 51, 52, 54, 56, 57, 59 } ,
                { 49, 51, 52, 54, 56, 58, 59 } ,
                { 49, 51, 53, 54, 56, 58, 59 } ,
                { 48, 49, 51, 53, 54, 56, 58 } ,
                { 48, 49, 51, 53, 55, 56, 58 } ,
                { 48, 50, 51, 53, 55, 56, 58 } ,
                { 48, 50, 51, 53, 55, 57, 58 } ,
                { 48, 50, 52, 53, 55, 57, 58 }
            };
            string[] chordName = new string[12]
            {
                "C  / Am",
                "G  / Em",
                "D  / Bm",
                "A  / F#m / G♭m",
                "E  / C#m / D♭m",
                "B  / G#m / A♭m",
                "F# / G♭ / D#m / E♭m",
                "C# / D♭ / A#m / B♭m",
                "G# / A♭ / Fm",
                "D# / E♭ / Cm",
                "A# / B♭ / Gm",
                "F  / Dm",
            };

            int findIndex = -1;
            for (int i = 0; i < 12; i++)
            {
                bool isAllContains = true;
                for (int j = 0; j < 7; j++)
                {
                    if(noteList_highPrio.Contains(chord[i, j]) == false)
                    {
                        isAllContains = false;
                        break;
                    }
                }

                if (isAllContains)
                {
                    findIndex = i;
                    break;
                }
            }
            
            if(findIndex != -1)
            {
                KeyLabel.Text = chordName[findIndex];
            }
        }


        private void backgroundWorker1_DoWork(object sender, DoWorkEventArgs e)
        {
            while (!backgroundWorker1.CancellationPending)
            {
                if (songKey != songKeyBefore)
                {
                    songKeyLabelText = songKeyCs[songKey].Text;
                }
                songKeyBefore = songKey;
            }
        }
        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            //スレッドの終了を待機
            backgroundWorker1.CancelAsync();
            Application.DoEvents();
        }

        private void pictureBox1_Paint(object sender, PaintEventArgs e)
        {
            if (currentImage != null)
            {
                //　画像を0,0に描画する
                e.Graphics.DrawImage(currentImage,
                    0, 0, currentImage.Width/2, currentImage.Height/2);
            }
        }

        private void PlayMidiFile_Click(object sender, EventArgs e)
        {
            LoadMidiName.Text = midi_filename;
            Program.Play(LoadMidiName.Text);

        }



        private void key1_Click(object sender, EventArgs e)
        {
            MidiEdit.Form1.songKey = 0;
            KeyLabel.Text = "C";
        }

        private void key2_Click(object sender, EventArgs e)
        {
            MidiEdit.Form1.songKey = 1;
            KeyLabel.Text = "C#";
        }

        private void key3_Click(object sender, EventArgs e)
        {
            MidiEdit.Form1.songKey = 2;
            KeyLabel.Text = "D";
        }

        private void key4_Click(object sender, EventArgs e)
        {
            MidiEdit.Form1.songKey = 3;
            KeyLabel.Text = "D#";
        }

        private void key5_Click(object sender, EventArgs e)
        {
            MidiEdit.Form1.songKey = 4;
            KeyLabel.Text = "E";
        }

        private void key6_Click(object sender, EventArgs e)
        {
            MidiEdit.Form1.songKey = 5;
            KeyLabel.Text = "F";
        }

        private void key7_Click(object sender, EventArgs e)
        {
            MidiEdit.Form1.songKey = 6;
            KeyLabel.Text = "F#";
        }

        private void key8_Click(object sender, EventArgs e)
        {
            MidiEdit.Form1.songKey = 7;
            KeyLabel.Text = "G";
        }

        private void key9_Click(object sender, EventArgs e)
        {
            MidiEdit.Form1.songKey = 8;
            KeyLabel.Text = "G#";
        }

        private void key10_Click(object sender, EventArgs e)
        {
            MidiEdit.Form1.songKey = 9;
            KeyLabel.Text = "A";
        }

        private void key11_Click(object sender, EventArgs e)
        {
            MidiEdit.Form1.songKey = 10;
            KeyLabel.Text = "A#";
        }

        private void key12_Click(object sender, EventArgs e)
        {
            MidiEdit.Form1.songKey = 11;
            KeyLabel.Text = "B";
        }






        public class MidiFileClass
        {
            public MidiFileClass()
            {
            }

            // ヘッダチャンク
            public int Format { get; set; } = 1;
            public int TicksPerBeat { get; set; } = 480;

            // トラックチャンク
            public List<MidiTrack> Tracks { get; } = new List<MidiTrack>();

            // ヘッダチャンクをバイト列に変換する
            public byte[] GetHeaderChunkBytes()
            {
                List<byte> bytes = new List<byte>();
                // ヘッダーチャンク識別子
                bytes.AddRange(new byte[] { 0x4D, 0x54, 0x68, 0x64 });
                // データ長
                bytes.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x06 });
                // フォーマット
                bytes.AddRange(GetBytes((short)Format, 2));
                // トラック数
                bytes.AddRange(GetBytes((short)Tracks.Count, 2));
                // 時間分解能
                //bytes.AddRange(GetBytes((short)TicksPerBeat, 2));
                bytes.Add(0x00);
                bytes.Add(0x60);
                

                return bytes.ToArray();
            }

            private byte[] GetBytes(int value, int byteCount)
            {
                List<byte> bytes = new List<byte>(BitConverter.GetBytes((short)value));
                for (int i = bytes.Count; i < byteCount; ++i)
                {
                    bytes.Insert(0, 0x0);
                }
                bytes.Reverse();
                return bytes.ToArray();
            }

            // トラックチャンクをバイト列に変換する
            private byte[] GetTrackChunkBytes()
            {
                List<byte> bytes = new List<byte>();
                foreach (var midiTrack in Tracks)
                {
                    bytes.AddRange(midiTrack.ToBytes());
                }
                return bytes.ToArray();
            }

            // MIDIファイル全体をバイト列に変換する
            public byte[] ToBytes()
            {
                List<byte> bytes = new List<byte>();
                bytes.AddRange(GetHeaderChunkBytes());
                bytes.AddRange(GetTrackChunkBytes());
                return bytes.ToArray();
            }
        }

        public class MidiTrack
        {
            protected List<MidiEvent> events = new List<MidiEvent>();

            public void AddEvent(MidiEvent midiEvent)
            {
                events.Add(midiEvent);
            }

            public virtual byte[] ToBytes()
            {
                List<byte> bytes = new List<byte>();

                // Track Chunk Header
                bytes.AddRange(Encoding.ASCII.GetBytes("MTrk"));
                bytes.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00 }); // データ長（後で書き換える）

                int headerSize = bytes.Count;


                // Track Events
                foreach (MidiEvent midiEvent in events)
                {
                    int deltaTime = midiEvent.DeltaTime;
                    byte[] deltaTimeBytes = {0};
                    //deltaTimeBytes = MidiUtility.EncodeVariableLengthValue(deltaTime);
                    if (deltaTime > 0)
                    {
                        //deltaTimeの数値を2バイトにする
                        byte[] deltaTimeBytes2 = { 0, 0 };

                        if (deltaTime <= 24 * 1)
                        {
                            //１６分音符　これが最短のノートの長さ分解能
                            deltaTimeBytes2[0] = 0x18;
                            deltaTimeBytes2[1] = 0x00;
                            bytes.Add(deltaTimeBytes2[0]);
                            bytes.AddRange(midiEvent.ToBytes());
                            continue;
                        }
                        else if (deltaTime > 24 * 1 && deltaTime <= 24 * 2)
                        {
                            //８分音符
                            deltaTimeBytes2[0] = 0x30;
                            deltaTimeBytes2[1] = 0x00;
                            bytes.Add(deltaTimeBytes2[0]);
                            bytes.AddRange(midiEvent.ToBytes());
                            continue;
                        }
                        else if (deltaTime > 24 * 2 && deltaTime <= 24 * 3)
                        {
                            deltaTimeBytes2[0] = 0x48;
                            deltaTimeBytes2[1] = 0x00;
                            bytes.Add(deltaTimeBytes2[0]);
                            bytes.AddRange(midiEvent.ToBytes());
                            continue;
                        }
                        else if (deltaTime > 24 * 3 && deltaTime <= 24 * 4)
                        {
                            //４分音符
                            deltaTimeBytes2[0] = 0x60;
                            deltaTimeBytes2[1] = 0x00;
                            bytes.Add(deltaTimeBytes2[0]);
                            bytes.AddRange(midiEvent.ToBytes());
                            continue;
                        }
                        else if (deltaTime > 24 * 4 && deltaTime <= 24 * 5)
                        {
                            deltaTimeBytes2[0] = 0x78;
                            deltaTimeBytes2[1] = 0x00;
                            bytes.Add(deltaTimeBytes2[0]);
                            bytes.AddRange(midiEvent.ToBytes());
                            continue;
                        }
                        else if (deltaTime > 24 * 5 && deltaTime <= 24 * 6)
                        {
                            deltaTimeBytes2[0] = 0x81;
                            deltaTimeBytes2[1] = 0x10;
                        }
                        else if (deltaTime > 24 * 6 && deltaTime <= 24 * 7)
                        {
                            deltaTimeBytes2[0] = 0x81;
                            deltaTimeBytes2[1] = 0x28;
                        }
                        else if (deltaTime > 24 * 7 && deltaTime <= 24 * 8)
                        {
                            deltaTimeBytes2[0] = 0x81;
                            deltaTimeBytes2[1] = 0x40;
                        }
                        else if (deltaTime > 48 * 4 && deltaTime <= 48 * 5)
                        {
                            deltaTimeBytes2[0] = 0x81;
                            deltaTimeBytes2[1] = 0x70;
                        }
                        else if (deltaTime > 48 * 5 && deltaTime <= 48 * 6)
                        {
                            deltaTimeBytes2[0] = 0x82;
                            deltaTimeBytes2[1] = 0x20;
                        }
                        else if (deltaTime > 48 * 6 && deltaTime <= 48 * 7)
                        {
                            deltaTimeBytes2[0] = 0x82;
                            deltaTimeBytes2[1] = 0x50;
                        }
                        else if (deltaTime > 48 * 7 && deltaTime <= 48 * 8)
                        {
                            deltaTimeBytes2[0] = 0x83;
                            deltaTimeBytes2[1] = 0x00;
                        }
                        bytes.AddRange(deltaTimeBytes2);
                        bytes.AddRange(midiEvent.ToBytes());
                    }
                    else
                    {
                        bytes.AddRange(deltaTimeBytes);
                        bytes.AddRange(midiEvent.ToBytes());
                    }
                }

                // End of Track Event
                byte[] endOfTrackEvent = new byte[] { 0x00, 0xFF, 0x2F, 0x00 };
                bytes.AddRange(endOfTrackEvent);

                int trackLength = bytes.Count - headerSize; // チャンクタイプとデータ長を除く
                bytes[4] = (byte)((trackLength >> 24) & 0xFF);
                bytes[5] = (byte)((trackLength >> 16) & 0xFF);
                bytes[6] = (byte)((trackLength >> 8) & 0xFF);
                bytes[7] = (byte)(trackLength & 0xFF);

                return bytes.ToArray();
            }
        }

        public abstract class MidiEvent
        {
            public int DeltaTime { get; set; }

            public abstract byte[] ToBytes();
        }

        public class NoteOnEvent : MidiEvent
        {
            private int channel;
            private int note;
            private int velocity;

            public NoteOnEvent(int channel, int note, int velocity)
            {
                this.channel = channel;
                this.note = note;
                this.velocity = velocity;
            }

            public override byte[] ToBytes()
            {
                List<byte> bytes = new List<byte>();
                bytes.Add((byte)(0x90 | (channel & 0x0F))); // ステータスバイト
                bytes.Add((byte)(note & 0x7F)); // データバイト
                bytes.Add((byte)(velocity & 0x7F)); // データバイト
                //bytes.Add((byte)note); // データバイト
                //bytes.Add((byte)velocity); // データバイト
                return bytes.ToArray();
            }
        }

        public class NoteOffEvent : MidiEvent
        {
            private int channel;
            private int note;

            public NoteOffEvent(int channel, int note, int deltaTime)
            {
                this.channel = channel;
                this.note = note;
                this.DeltaTime = deltaTime;
            }

            public override byte[] ToBytes()
            {
                List<byte> bytes = new List<byte>();
                bytes.Add((byte)(0x80 | (channel & 0x0F))); // ステータスバイト
                bytes.Add((byte)(note & 0x7F)); // データバイト
                bytes.Add(0);
                return bytes.ToArray();
            }
        }

        public class ProgramChangeEvent : MidiEvent
        {
            private int channel;
            private int program;

            public ProgramChangeEvent(int channel, int program)
            {
                this.channel = channel;
                this.program = program;
            }

            public override byte[] ToBytes()
            {
                List<byte> bytes = new List<byte>();
                bytes.Add((byte)(0xC0 | (channel & 0x0F))); // ステータスバイト
                bytes.Add((byte)(program & 0x7F)); // データバイト
                return bytes.ToArray();
            }
        }

        public class BankMsbEvent : MidiEvent
        {
            private int channel;
            private int value;

            public BankMsbEvent(int channel, int value)
            {
                this.channel = channel;
                this.value = value;
            }

            public override byte[] ToBytes()
            {
                List<byte> bytes = new List<byte>();
                bytes.Add((byte)(0xB0 | (channel & 0x0F))); // ステータスバイト
                bytes.Add(0x00);
                bytes.Add((byte)value); // データバイト
                return bytes.ToArray();
            }
        }

        public class BankLsbEvent : MidiEvent
        {
            private int channel;
            private int value;

            public BankLsbEvent(int channel, int value)
            {
                this.channel = channel;
                this.value = value;
            }

            public override byte[] ToBytes()
            {
                List<byte> bytes = new List<byte>();
                bytes.Add((byte)(0xB0 | (channel & 0x0F))); // ステータスバイト
                bytes.Add(0x20);
                bytes.Add((byte)value); // データバイト
                return bytes.ToArray();
            }
        }

        public class ExpressionEvent : MidiEvent
        {
            private int channel;
            private int value;

            public ExpressionEvent(int channel, int value)
            {
                this.channel = channel;
                this.value = value;
            }

            public override byte[] ToBytes()
            {
                List<byte> bytes = new List<byte>();
                bytes.Add((byte)(0xB0 | (channel & 0x0F))); // ステータスバイト
                bytes.Add(0x0B);
                bytes.Add((byte)value); // データバイト
                return bytes.ToArray();
            }
        }

        public class TrackNameEvent : MidiEvent
        {
            public string TrackName { get; set; } = string.Empty;
            public TrackNameEvent(string trackName)
            {
                TrackName = trackName;
            }

            public override byte[] ToBytes()
            {
                List<byte> bytes = new List<byte>();
                bytes.Add(0xFF);
                bytes.Add(0x03);
                var trackNameBytes = ASCIIEncoding.ASCII.GetBytes(TrackName);
                bytes.Add((byte)(trackNameBytes.Length+2)); // 長さ
                bytes.AddRange(trackNameBytes);

                bytes.Add(0x00);
                bytes.Add(0x00);

                return bytes.ToArray();
            }
        }

        static int try_count = 1;
        private void SaveMidiFile_Click(object sender, EventArgs e)
        {
            DebugWindows.Text = "==== 和音生成_" + try_count.ToString() + " ====\n" + DebugWindows.Text;
            try_count++;

            if (midi_filename == "")
            {
                DebugWindows.Text = "MIDIファイルが読み込まれていません\n" + DebugWindows.Text;
                return;
            }

            ChordMake_Click(null, null);

            //パターン用MIDI読み込み
            if (midi_filename_pattern != null)
            {
                ChordMake_Click_Pattern(null, null);
            }

            if (isFixChordMidi)
            {
                DebugWindows.Text = "修正したMIDIの和音を使用\n" + DebugWindows.Text;
            }
            else
            {
                DebugWindows.Text = "和音生成開始\n" + DebugWindows.Text;
            }

            // MIDIファイルを作成
            MidiFileClass midiFileClass = new MidiFileClass();
            DebugWindows.Text = "MIDIファイルを作成\n" + DebugWindows.Text;
            MidiFileClass midiFileClassPattern;

            midiFileClassPattern = new MidiFileClass();
            DebugWindows.Text = "MIDIパターンファイルを作成\n" + DebugWindows.Text;


            int trackCount = 1; //最大１６
            for (int trackIndex = 0; trackIndex < trackCount; trackIndex++)
            {
                var channel = trackIndex;
                MidiTrack track = new MidiTrack();
                track.AddEvent(new TrackNameEvent("Sytrus"));
                //track.AddEvent(new TrackNameEvent($"Track_{trackIndex}"));

                // プログラムチェンジイベントを追加
                //track.AddEvent(new BankMsbEvent(channel, 0));
                //track.AddEvent(new BankLsbEvent(channel, 0));
                //ProgramChangeEvent programChangeEvent = new ProgramChangeEvent(channel, 0);
                //track.AddEvent(programChangeEvent);
                //track.AddEvent(new ExpressionEvent(channel, 100));

                midiFileClass.Tracks.Add(track);


                MidiTrack trackPattern = new MidiTrack();
                trackPattern.AddEvent(new TrackNameEvent("Sytrus"));
                midiFileClassPattern.Tracks.Add(trackPattern);
            }

            {
                var track = midiFileClass.Tracks[0];
                var trackPattern = midiFileClassPattern.Tracks[0];
                int channel = 0;

                // スケールを演奏するためのノートオン/ノートオフイベントを追加
                const int maxNote = 1024;
                const int waonMax = 4; //3+1 1は長さ
                int[,] scaleScore = new int[maxNote, waonMax];


                //まず、出てきた音を覚えておく
                noteList.Clear();
                noteList_highPrio.Clear();
                noteList_Chord.Clear();
                for (int i = 0; i < maxNote; i++)
                {
                    if (MidiSequencer.playNoteArray[i] == 0)
                    {
                        break;
                    }
                    int note = MidiSequencer.playNoteArray[i];

                    //何度も出てきていれば優先リストに追加
                    int chouhukuCount = 0;
                    for(int k=0; k< noteList.Count; k++)
                    {
                        if(noteList[k] == note)
                        {
                            chouhukuCount++;
                        }
                        if (chouhukuCount >= highPrio_border)
                        {
                            if (!noteList_highPrio.Contains(note))
                            {
                                noteList_highPrio.Add(note);
                                if (!noteList_highPrio.Contains(note - 12)) noteList_highPrio.Add(note - 12);
                                if (!noteList_highPrio.Contains(note + 12)) noteList_highPrio.Add(note + 12);
                                if (!noteList_highPrio.Contains(note - 24)) noteList_highPrio.Add(note - 24);
                                if (!noteList_highPrio.Contains(note + 24)) noteList_highPrio.Add(note + 24);
                            }

                            //＋７の位置が上段
                            if (!noteList_highPrio.Contains(note + 7))
                            {
                                if (!isFixChordMidi)
                                {
                                    noteList_highPrio.Add(note + 7);
                                    noteList_highPrio.Add(note + 7 - 12);
                                    noteList_highPrio.Add(note + 7 + 12);
                                    noteList_highPrio.Add(note + 7 - 24);
                                    noteList_highPrio.Add(note + 7 + 24);
                                }
                            }
                        }
                    }
                    noteList_highPrio.Sort();

                    noteList.Add(note);
                    if (!isFixChordMidi)
                    {
                        //1オクターブ上下のも追加しておく
                        noteList.Add(note - 12);
                        noteList.Add(note + 12);
                        //いちおう２オクターブも
                        noteList.Add(note - 24);
                        noteList.Add(note + 24);
                    }
                }


                //まずは３回以上出てきた音で、確定している和音を生成する
                DebugWindows.Text = "高優先度のリストから和音生成開始\n" + DebugWindows.Text;
                for (int i = 0; i < maxNote; i++)
                {
                    if (MidiSequencer.playNoteArray[i] == 0)
                    {
                        break;
                    }
                    int noteLow = MidiSequencer.playNoteArray[i];

                    //先に上段
                    MakeChordPrev(2, noteLow, -1);

                    //中段
                    MakeChordPrev(1, noteLow, -1);

                }


                //テスト
                if (!isFixChordMidi)
                {
                    DebugWindows.Text = "低優先度のリストから和音生成開始\n" + DebugWindows.Text; ;
                    for (int i = 0; i < maxNote; i++)
                    {
                        if (MidiSequencer.playNoteArray[i] == 0)
                        {
                            break;
                        }

                        int noteLow = MidiSequencer.playNoteArray[i];
                        int length = MidiSequencer.playLengthArray[i];

                        scaleScore[i, 0] = noteLow;
                        scaleScore[i, 1] = MakeChord(1, noteLow, -1);

                        int noteMid = scaleScore[i, 1];
                        scaleScore[i, 2] = MakeChord(2, noteLow, noteMid);

                        //長さ
                        scaleScore[i, 3] = length;
                    }
                }
                else
                {
                    DebugWindows.Text = "修正後のMIDIから和音を設定\n" + DebugWindows.Text; ;
                    for (int i = 0; i < maxNote; i++)
                    {
                        if (MidiSequencer.playNoteArray[i] == 0)
                        {
                            break;
                        }

                        int length = MidiSequencer.playLengthArray[i * 3];

                        List<int> note_tmp = new List<int> { };     //一度でも出てきた音
                        note_tmp.Add(MidiSequencer.playNoteArray[i * 3 + 0]);
                        note_tmp.Add(MidiSequencer.playNoteArray[i * 3 + 1]);
                        note_tmp.Add(MidiSequencer.playNoteArray[i * 3 + 2]);
                        note_tmp.Sort();

                        scaleScore[i, 0] = note_tmp[0];
                        scaleScore[i, 1] = note_tmp[1];
                        scaleScore[i, 2] = note_tmp[2];

                        //長さ
                        scaleScore[i, 3] = length;
                    }
                }


                DebugWindows.Text = "和音の配置\n" + DebugWindows.Text;
                int velocity = 100;
                int duration = 480; // 四分音符の長さ

                for (int i = 0; i < maxNote; i++)
                {
                    if (scaleScore[i, 0] == 0)
                    {
                        break;
                    }

                    int[] scale =
                    {
                        scaleScore[i, 0], scaleScore[i, 1], scaleScore[i, 2]
                    };
                    
                    foreach (int note in scale)
                    {
                        NoteOnEvent noteOnEvent = new NoteOnEvent(channel, note, velocity);
                        track.AddEvent(noteOnEvent);
                        noteOnEvent.DeltaTime = 0;
                    }

                    int domiso_no_do = 0;
                    foreach (int note in scale)
                    {
                        if (domiso_no_do == 0)
                        {
                            duration = scaleScore[i, 3];

                            //パターン生成
                            if (midi_filename_pattern != null)
                            {
                                int bass_low = scaleScore[i, 0];    //ベースの音から開始


                                int duration_Sum = 0;   //パターンのノートの長さの合計
                                for (int patternNum = 0; patternNum < 16/*MidiSequencer.playNoteArrayPattern.Count*/; patternNum++)
                                {
                                    //FLのC4のノートが48
                                    int note_Pattern = MidiSequencer.playNoteArrayPattern[patternNum];
                                    int duration_Pattern = MidiSequencer.playLengthArrayPattern[patternNum];

                                    int pattern_sabun = bass_low - 48;              //コードの下段とパターンの差分
                                    int note_Pattern_sabun = note_Pattern - 48;     //パターン自体のノートの段差
                                    note_Pattern += pattern_sabun;

                                    //パターンに使用されているノートがあるか検索する
                                    bool patarnPartsFind = false;
                                    if (pattern_sabun == 0)
                                    {
                                        patarnPartsFind = true;     //ベースの音だったらそのままでいい
                                    }
                                    //使用するコードから検索する
                                    for (int noteListNum = 0; noteListNum < 3; noteListNum++)
                                    {
                                        if (scaleScore[i, noteListNum] == note_Pattern)
                                        {
                                            patarnPartsFind = true;
                                            break;
                                        }
                                    }

                                    //高優先度から検索する
                                    for (int noteListNum = 0; noteListNum < noteList_highPrio.Count; noteListNum++)
                                    {
                                        if (noteList_highPrio[noteListNum] == bass_low)
                                        {
                                            int index = noteListNum + note_Pattern_sabun;
                                            if (index < noteList_highPrio.Count)
                                            {
                                                note_Pattern = noteList_highPrio[index];

                                                patarnPartsFind = true;
                                            }
                                            else
                                            {
                                                DebugWindows.Text = "highPrioのチェックで要素数を越えた\n" + DebugWindows.Text;
                                            }
                                            break;
                                        }
                                    }
                                    //高優先度から近いものに近付ける
                                    if (!patarnPartsFind)
                                    {
                                        for (int noteListNum = 0; noteListNum < noteList_highPrio.Count; noteListNum++)
                                        {
                                            if (noteList_highPrio[noteListNum] == note_Pattern + 1)
                                            {
                                                note_Pattern = note_Pattern + 1;
                                                patarnPartsFind = true;
                                                break;
                                            }
                                        }
                                    }
                                    //低優先度から検索する
                                    for (int noteListNum = 0; noteListNum < noteList.Count; noteListNum++)
                                    {
                                        if (noteList[noteListNum] == note_Pattern)
                                        {
                                            patarnPartsFind = true;
                                            break;
                                        }
                                    }
                                    //低優先度にもなければ近いものに近付ける
                                    for (int highPrioLoop = 0; highPrioLoop < 2; highPrioLoop++)
                                    {
                                        if (!patarnPartsFind)
                                        {
                                            note_Pattern += 1;
                                            for (int noteListNum = 0; noteListNum < noteList_highPrio.Count; noteListNum++)
                                            {
                                                if (noteList_highPrio[noteListNum] == note_Pattern)
                                                {
                                                    patarnPartsFind = true;
                                                    break;
                                                }
                                            }
                                        }
                                    }
                                    if (!patarnPartsFind)
                                    {
                                        DebugWindows.Text = "Pattern生成に必要なノートが見つからなかった\n" + DebugWindows.Text;
                                    }

                                    if (note_Pattern <= 0)
                                    {
                                        //これ以上ノートがない
                                        break;
                                    }
                                    bool isEnd = false;
                                    if (duration_Sum + duration_Pattern >= duration)
                                    {
                                        //長さが元のベースの長さを越えた
                                        duration_Pattern = duration - duration_Sum;
                                        isEnd = true;
                                    }

                                    NoteOnEvent noteOnEventPattern = new NoteOnEvent(channel, note_Pattern, velocity);
                                    trackPattern.AddEvent(noteOnEventPattern);

                                    NoteOffEvent noteOffEventPattern = new NoteOffEvent(channel, note_Pattern, duration_Pattern);
                                    trackPattern.AddEvent(noteOffEventPattern);
                                    duration_Sum += duration_Pattern;

                                    if (isEnd)
                                    {
                                        break;
                                    }
                                }
                                //noteList_highPrio
                            }
                        }
                        else
                        {
                            duration = 0;   // ノートを同じ長さにしたいので
                        }
                        NoteOffEvent noteOffEvent = new NoteOffEvent(channel, note, duration);
                        track.AddEvent(noteOffEvent);
                        domiso_no_do++;
                    }
                }
            }

            //highPrioで使用する音を画面のピアノに反映と、調の特定
            {
                for (int noteListNum = 0; noteListNum < NOTE_MAX; noteListNum++)
                {
                    noteStatus[noteListNum] = 0;
                }
                for (int noteListNum = 0; noteListNum < noteList_highPrio.Count; noteListNum++)
                {
                    noteStatus[noteList_highPrio[noteListNum]] = 1;
                }
                PianoColorChange();
                KeyIdentification();
            }

            // MIDIファイルを書き出し
            if (!isFixChordMidi)
            {
                string outputName = midi_filename;
                byte[] bytes = midiFileClass.ToBytes();
                int strlen = midi_filename.Length;
                outputName = midi_filename.Substring(0, strlen - 4);
                outputName += "_Chord.mid";
                using (FileStream fileStream = new FileStream(outputName, FileMode.Create))
                {
                    fileStream.Write(bytes, 0, bytes.Length);

                    DebugWindows.Text = outputName + "\n" + DebugWindows.Text;
                    DebugWindows.Text = "MIDIファイル出力完了\n" + DebugWindows.Text;
                }
            }

            // パターンMIDIファイルを書き出し
            if (midi_filename_pattern != null)
            {
                string outputName = midi_filename;
                byte[] bytes = midiFileClassPattern.ToBytes();
                int strlen = midi_filename.Length;
                outputName = midi_filename.Substring(0, strlen - 4);

                string patternFileName = Path.GetFileName(midi_filename_pattern);
                outputName += "_" + patternFileName /*+ ".mid"*/;
                
                using (FileStream fileStream = new FileStream(outputName, FileMode.Create))
                {
                    fileStream.Write(bytes, 0, bytes.Length);

                    DebugWindows.Text = outputName + "\n" + DebugWindows.Text;
                    DebugWindows.Text = "MIDIファイル出力完了\n" + DebugWindows.Text;
                }
            }
        }

        public int MakeChord(int chordNum, int noteLow, int noteMid)
        {
            int addNote = 0;
            //中段
            if (chordNum == 1)
            {
                for (int i = 0; i < noteList.Count(); i++) {
                    //まずは可能性の高いほうから
                    for (int k = 0; k < noteList_Chord.Count(); k++)
                    {
                        if (noteLow + 3 == noteList_Chord[k])
                        {
                            addNote = noteLow + 3;
                            return addNote;
                        }
                        else if (noteLow + 4 == noteList_Chord[k])
                        {
                            addNote = noteLow + 4;
                            return addNote;
                        }
                    }

                    //可能性の低いほう
                    if (noteLow + 3 == noteList.ElementAt(i))
                    {
                        addNote = noteLow + 3;
                        if (!noteList.Contains(addNote))
                        {
                            noteList.Add(addNote);
                        }
                        return addNote;
                    }
                    else if (noteLow + 4 == noteList.ElementAt(i))
                    {
                        addNote = noteLow + 4;
                        if (!noteList.Contains(addNote))
                        {
                            noteList.Add(addNote);
                        }
                        return addNote;
                    }
                }

                //見つからなかったので上下で推測
                if (songKey == 0)
                {
                    //C

                }
                return noteLow + 3; //見つからなかったのでとりあえず３つ上を返す。。。
            }

            //上段
            if (chordNum == 2)
            {
                //２つ分かっているので推測
                int sabun = noteMid - noteLow;
                if(sabun == 3)
                {
                    addNote = noteMid + 4;
                    if (!noteList.Contains(addNote))
                    {
                        noteList.Add(addNote);
                    }
                    return addNote;
                }
                else if (sabun == 4)
                {
                    addNote = noteMid + 3;
                    if (!noteList.Contains(addNote))
                    {
                        noteList.Add(addNote);
                    }
                    return addNote;
                }
            }

            return 0;   //エラー
        }


        public int MakeChordPrev(int chordNum, int noteLow, int noteMid)
        {
            int addNote = 0;
            //中段
            if (chordNum == 1)
            {
                for (int i = 0; i < noteList_highPrio.Count(); i++)
                {
                    if (noteLow + 3 == noteList_highPrio.ElementAt(i))
                    {
                        addNote = noteLow + 3;
                        if (!noteList_Chord.Contains(addNote))
                        {
                            noteList_Chord.Add(addNote);
                        }
                        return addNote;
                    }
                    if (noteLow + 4 == noteList_highPrio.ElementAt(i))
                    {
                        addNote = noteLow + 4;
                        if (!noteList_Chord.Contains(addNote))
                        {
                            noteList_Chord.Add(addNote);
                        }
                        return addNote;
                    }
                }
                return -1; //見つからなかった
            }

            //上段
            if (chordNum == 2)
            {
                //上段は７つ上
                addNote = noteLow + 7;
                for (int i = 0; i < noteList_highPrio.Count(); i++)
                {
                    if (addNote == noteList_highPrio.ElementAt(i))
                    {
                        if (!noteList_Chord.Contains(addNote))
                        {
                            noteList_Chord.Add(addNote);
                        }
                    }
                }
                return addNote;

                ////２つ分かっているので推測
                //int sabun = noteMid - noteLow;
                //if (sabun == 3)
                //{
                //    addNote = noteMid + 4;
                //    if (!noteList_Chord.Contains(addNote))
                //    {
                //        noteList_Chord.Add(addNote);
                //    }
                //    return addNote;
                //}
                //else if (sabun == 4)
                //{
                //    addNote = noteMid + 3;
                //    if (!noteList_Chord.Contains(addNote))
                //    {
                //        noteList_Chord.Add(addNote);
                //    }
                //    return addNote;
                //}
            }
            return -1;   //エラー
        }

        private void ChordMake_Click(object sender, EventArgs e)
        {
            DebugWindows.Text = "MIDI解析開始\n" + DebugWindows.Text;
            LoadMidiName.Text = midi_filename;
            Program.MidiInfoSet(LoadMidiName.Text);
            DebugWindows.Text = "MIDI解析完了\n" + DebugWindows.Text;
        }
        private void ChordMake_Click_Pattern(object sender, EventArgs e)
        {
            DebugWindows.Text = "パターンMIDI解析開始\n" + DebugWindows.Text;
            LoadMidiName2.Text = midi_filename_pattern;
            Program.MidiInfoSetPattern(LoadMidiName2.Text);
            DebugWindows.Text = "パターンMIDI解析完了\n" + DebugWindows.Text;
        }

        private void numericUpDown1_ValueChanged(object sender, EventArgs e)
        {
            highPrio_border = (int)numericUpDown1.Value;
        }

        private void label5_Click(object sender, EventArgs e)
        {

        }

        private void Form1_Load(object sender, EventArgs e)
        {
            this.Icon = new System.Drawing.Icon("icon.ico");
        }

        private void label11_Click(object sender, EventArgs e)
        {

        }

        private void load_midi_button2_Click(object sender, EventArgs e)
        {
            string midiFileName = "";

            //OpenFileDialogクラスのインスタンスを作成
            OpenFileDialog ofd = new OpenFileDialog();

            //はじめのファイル名を指定する
            //はじめに「ファイル名」で表示される文字列を指定する
            ofd.FileName = "default.midi";
            //はじめに表示されるフォルダを指定する
            //指定しない（空の文字列）の時は、現在のディレクトリが表示される
            ofd.InitialDirectory = @"C:\";
            //[ファイルの種類]に表示される選択肢を指定する
            //指定しないとすべてのファイルが表示される
            ofd.Filter = "midiファイル(*.midi;*.mid)|*.midi;*.mid|すべてのファイル(*.*)|*.*";
            //[ファイルの種類]ではじめに選択されるものを指定する
            ofd.FilterIndex = 1;
            //タイトルを設定する
            ofd.Title = "開くファイルを選択してください";
            //ダイアログボックスを閉じる前に現在のディレクトリを復元するようにする
            ofd.RestoreDirectory = true;
            //存在しないファイルの名前が指定されたとき警告を表示する
            //デフォルトでTrueなので指定する必要はない
            ofd.CheckFileExists = true;
            //存在しないパスが指定されたとき警告を表示する
            //デフォルトでTrueなので指定する必要はない
            ofd.CheckPathExists = true;

            //ダイアログを表示する
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                //OKボタンがクリックされたとき、選択されたファイル名を表示する
                Console.WriteLine(ofd.FileName);
                midiFileName = ofd.FileName;

                midi_filename_pattern = midiFileName;
                LoadMidiName2.Text = midiFileName;
                DebugWindows.Text = midiFileName + " パターン読み込み\n" + DebugWindows.Text;
            }
        }

        private void logClear_Click(object sender, EventArgs e)
        {
            DebugWindows.Text = "";
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            isFixChordMidi = checkBox1.Checked;
        }

        private void checkBox2_CheckedChanged(object sender, EventArgs e)
        {
            isChordNoteOnly = checkBox2.Checked;
        }
    }
}
