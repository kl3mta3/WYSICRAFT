package com.wysicraft.runtime.api;

import java.util.HashMap;
import java.util.Map;
import java.util.UUID;

/** Per-player allowance of server-thread time for server scripts, as a token bucket refilled every tick.
 * Stops one player's rapid events (e.g. text_changed on every keystroke) from stalling the server.
 * A run that overspends (up to the 2 s script timeout) puts the player into debt that must refill first. */
public final class ScriptBudget {
    public static final long REFILL_PER_TICK = 5_000_000L;  // 5 ms per tick = 100 ms of script time per second
    public static final long CAPACITY = 250_000_000L;       // burst allowance
    public static final long MIN_COST = 1_000_000L;         // every run costs at least 1 ms
    public static final long WARN_INTERVAL_TICKS = 100;
    private static final class Bucket { long available = CAPACITY, tick, warned = -WARN_INTERVAL_TICKS; Bucket(long tick) { this.tick = tick; } }
    private final Map<UUID,Bucket> buckets = new HashMap<>();

    /** True when the player may run a script now. */
    public boolean tryStart(UUID player, long tick) {
        Bucket b = buckets.computeIfAbsent(player, key -> new Bucket(tick));
        long elapsed = Math.max(0, tick - b.tick);
        b.available = Math.min(CAPACITY, b.available + elapsed * REFILL_PER_TICK);
        b.tick = tick;
        return b.available > 0;
    }
    public void charge(UUID player, long nanos) { Bucket b = buckets.get(player); if (b != null) b.available -= Math.max(MIN_COST, nanos); }
    /** True at most once per warning interval, so a throttled player can't flood the log. */
    public boolean shouldWarn(UUID player, long tick) {
        Bucket b = buckets.get(player); if (b == null || tick - b.warned < WARN_INTERVAL_TICKS) return false;
        b.warned = tick; return true;
    }
    public void forget(UUID player) { buckets.remove(player); }
}
