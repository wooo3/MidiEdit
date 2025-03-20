using NAudio.Midi;
using System.Collections.Generic;
using System.Linq;

//< 名前空間は略 >

// これは曲再生の際に用いるだけのクラスであるから、可視性は internal としている
internal class TempoData
{
    // ファイル内にテンポ指定がない場合は 120 bpm = 500 000 mpq とする.
    private const int DEFAULT_MPQ = 500_000;
    // mpqStack[n] = n 個目の Set Tempo イベントが持つ MPQ
    private readonly int[] mpqStack;
    // cumulativeTicks[n] = 曲の先頭から、n 個目の Set Tempo イベントが発生するまでの時間 (Ticks)
    private readonly long[] cumulativeTicks;
    // cumulativeMicroseconds[n] = 曲の先頭から、n 個目の Set Tempo イベントが発生するまでの時間 (us)
    private readonly long[] cumulativeMicroseconds;

    // 分解能(四分音符ひとつは何 Tick であるか)
    public int Resolution { get; }

    // 再生に当たって、NAudio.Midi.MidiEventCollection は実質的に Midi ファイルとして見なせる
    public TempoData(MidiEventCollection midiEvents)
    {
        // Pulses Per Quater note
        int resolution = midiEvents.DeltaTicksPerQuarterNote;

        // TempoEvent のみを抜き出す (イベントは AbsoluteTime の昇順で並んでいる)
        // Set Tempo イベントは 0 番トラックにのみ現れるはずなので、midiEvents[0] のみから探す
        List<(long tick, TempoEvent message)> tempoEvents = midiEvents[0].Where(evt => evt is TempoEvent)
                            .Select(evt => (tick: evt.AbsoluteTime, message: (TempoEvent)evt))
                            .ToList();

        if ((tempoEvents.Count == 0) || (tempoEvents[0].tick != 0L))
        {
            // 先頭にテンポ指定がない場合はデフォルト値を入れる
            tempoEvents.Insert(0, (0L, new TempoEvent(DEFAULT_MPQ, 0)));
        }

        this.mpqStack = new int[tempoEvents.Count];
        this.cumulativeTicks = new long[tempoEvents.Count];
        this.cumulativeMicroseconds = new long[tempoEvents.Count];

        // 0 Tick 時点での値を先に入れる
        mpqStack[0] = tempoEvents[0].message.MicrosecondsPerQuarterNote;
        cumulativeTicks[0] = cumulativeMicroseconds[0] = 0L;

        int pos = 1;
        foreach ((long tick, TempoEvent message) in tempoEvents.Skip(1))
        {
            cumulativeTicks[pos] = tick;
            // deltaTick = 前回の Set Tempo からの時間 (Ticks)
            long deltaTick = tick - cumulativeTicks[pos - 1];
            mpqStack[pos] = message.MicrosecondsPerQuarterNote;
            // deltaMicroseconds = 前回の Set Tempo からの時間 (us)
            // <= MPQ = mpqStack[pos - 1] で deltaTick だけ経過している
            long deltaMicroseconds = TicksToMicroseconds(deltaTick, mpqStack[pos - 1], resolution);
            cumulativeMicroseconds[pos] = cumulativeMicroseconds[pos - 1] + deltaMicroseconds;

            ++pos;
        }

        this.Resolution = resolution;
    }// Constructor

    public long MicrosecondsToTicks(long us)
    {
        // 曲の開始から us[マイクロ秒] 経過した時点は、
        // 曲の開始から 何Ticks 経過した時点であるかを計算する

        int index = GetIndexFromMicroseconds(us);

        // 現在の MPQ は mpq である
        int mpq = mpqStack[index];

        // 直前のテンポ変更があったのは cumUs(マイクロ秒) 経過した時点であった
        long cumUs = cumulativeMicroseconds[index];
        // 直前のテンポ変更があったのは cumTicks(Ticks) 経過した時点であった
        long cumTicks = cumulativeTicks[index];

        // 直前のテンポ変更から deltaUs(マイクロ秒)が経過している
        long deltaUs = us - cumUs;
        // 直前のテンポ変更から deltaTicks(Ticks)が経過している
        long deltaTicks = MicrosecondsToTicks(deltaUs, mpq, Resolution);

        return cumTicks + deltaTicks;
    }

    private int GetIndexFromMicroseconds(long us)
    {
        // 指定された時間(マイクロ秒)時点におけるインデックスを二分探索で探す
        int lo = -1;
        int hi = cumulativeMicroseconds.Length;
        while ((hi - lo) > 1)
        {
            int m = hi - (hi - lo) / 2;
            if (cumulativeMicroseconds[m] <= us) lo = m;
            else hi = m;
        }
        return lo;
    }

    private static long MicrosecondsToTicks(long us, long mpq, int resolution)
    {
        // 時間(マイクロ秒)を時間(Tick)に変換する
        return us * resolution / mpq;
    }

    private static long TicksToMicroseconds(long tick, long mqp, int resolution)
    {
        // 時間(Tick)を時間(マイクロ秒)に変換する
        return tick * mqp / resolution;
    }

}// class TempoData