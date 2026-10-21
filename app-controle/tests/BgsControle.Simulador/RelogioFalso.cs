namespace BgsControle.Simulador;

/// <summary>
/// Um <see cref="TimeProvider"/> com tempo falso: só anda com <see cref="Avancar"/>. Os timers
/// (e o <c>Task.Delay(t, relogio)</c>) disparam em ordem, na thread de quem avança.
/// </summary>
public sealed class RelogioFalso : TimeProvider
{
    private readonly object _trava = new();
    private readonly List<TimerFalso> _timers = [];
    private DateTimeOffset _agora;

    public RelogioFalso(DateTimeOffset? inicio = null) =>
        _agora = inicio ?? new DateTimeOffset(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow()
    {
        lock (_trava)
            return _agora;
    }

    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override long GetTimestamp() => GetUtcNow().UtcTicks;

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new TimerFalso(this, callback, state);
        lock (_trava)
            _timers.Add(timer);
        timer.Change(dueTime, period);
        return timer;
    }

    /// <summary>Anda o tempo e dispara, em ordem, os timers que vencem no caminho.</summary>
    public void Avancar(TimeSpan quanto)
    {
        if (quanto < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(quanto));
        DateTimeOffset alvo;
        lock (_trava)
            alvo = _agora + quanto;
        while (true)
        {
            TimerFalso? proximo;
            lock (_trava)
            {
                proximo = _timers.Where(t => t.Vence is { } v && v <= alvo).MinBy(t => t.Vence);
                if (proximo is null)
                {
                    _agora = alvo;
                    return;
                }
                _agora = proximo.Vence!.Value;
                proximo.Vence = proximo.Periodo is { } p ? _agora + p : null;
            }
            proximo.Disparar();
        }
    }

    private void Remover(TimerFalso timer)
    {
        lock (_trava)
            _timers.Remove(timer);
    }

    private sealed class TimerFalso(RelogioFalso relogio, TimerCallback callback, object? state) : ITimer
    {
        /// <summary>Quando dispara (null = parado). Mexido só com a trava do relógio.</summary>
        public DateTimeOffset? Vence { get; set; }

        public TimeSpan? Periodo { get; private set; }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            lock (relogio._trava)
            {
                Vence = dueTime == Timeout.InfiniteTimeSpan ? null : relogio._agora + dueTime;
                Periodo = period == Timeout.InfiniteTimeSpan || period == TimeSpan.Zero ? null : period;
            }
            return true;
        }

        public void Disparar() => callback(state);

        public void Dispose()
        {
            lock (relogio._trava)
                Vence = null;
            relogio.Remover(this);
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
