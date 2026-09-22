package com.wysicraft.runtime;
import com.wysicraft.runtime.api.ScriptBudget;
import org.junit.jupiter.api.Test;
import java.util.UUID;
import static org.junit.jupiter.api.Assertions.*;
class ScriptBudgetTests {
    @Test void throttlesAfterBurstAndRefillsOverTime() {
        var budget=new ScriptBudget();var player=UUID.randomUUID();var other=UUID.randomUUID();
        int runs=0;while(budget.tryStart(player,0)){budget.charge(player,10_000_000L);runs++;}
        assertEquals(25,runs,"250 ms burst at 10 ms per run");
        assertTrue(budget.tryStart(other,0),"players have separate budgets");
        assertFalse(budget.tryStart(player,0),"spent budget blocks further runs this tick");
        assertTrue(budget.tryStart(player,1),"the next tick refills 5 ms");
    }
    @Test void timedOutRunCreatesDebt() {
        var budget=new ScriptBudget();var player=UUID.randomUUID();
        assertTrue(budget.tryStart(player,0));budget.charge(player,2_000_000_000L);
        assertFalse(budget.tryStart(player,100));assertTrue(budget.tryStart(player,400));
        assertTrue(budget.shouldWarn(player,400));assertFalse(budget.shouldWarn(player,450));
    }
}
