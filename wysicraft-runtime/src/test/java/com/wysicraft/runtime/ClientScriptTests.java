package com.wysicraft.runtime;
import com.wysicraft.runtime.api.*;
import org.junit.jupiter.api.Test;
import java.nio.file.*;
import java.util.*;
import static org.junit.jupiter.api.Assertions.*;

class ClientScriptTests {
    @Test void serverEngineUsesRestrictedDataAndActions() {
        var host=new Host(){ public String query(String operation,String argument) { return operation.equals("player_inventory")?"[{\"item\":\"minecraft:apple\",\"count\":4,\"name\":\"Apple\"}]":"Test player"; } };
        ClientJavaScript.INSTANCE.execute(Scripts.Side.SERVER,"function refresh(ctx){ctx.ui.setItems('list',ctx.player.getInventory());ctx.server.runCommand('say ready');ctx.ui.open('main');}","refresh",host);
        assertTrue(host.values.get("set_value:list").contains("minecraft:apple"));assertEquals("say ready",host.values.get("command:"));assertEquals("main",host.values.get("open_ui:"));
        assertThrows(Exception.class,()->ClientJavaScript.INSTANCE.execute(Scripts.Side.CLIENT,"ctx.server.runCommand('say forbidden')","",new Host()));
    }
    @Test void setItemApiAndInvalidResource() {
        var host = new Host();
        ClientJavaScript.INSTANCE.execute(Scripts.Side.CLIENT,"ctx.ui.setItem('slot', 'minecraft:diamond'); ui.getElement('other').setItem('example:custom_item');", "", host);
        assertEquals("minecraft:diamond", host.values.get("set_item:slot"));
        assertEquals("example:custom_item", host.values.get("set_item:other"));
        host.values.clear();
        assertThrows(Exception.class, () -> ClientJavaScript.INSTANCE.execute(Scripts.Side.CLIENT,"ui.setItem('slot','minecraft:apple'); ui.setItem('slot','Bad ID');", "", host));
        assertTrue(host.values.isEmpty(), "Invalid script output must not be applied");
    }
    static class Host implements Scripts.Context {
        Map<String,String> values = new HashMap<>();
        public String getVariable(String name) { return values.getOrDefault(name,""); }
        public void setVariable(String name,String value) { values.put(name,value); }
        public void message(String text) {}
        public void action(String type,String target,String value) { values.put(type+":"+target,value); }
    }
    @Test void actualPanelScriptAndInvalidJson() throws Exception {
        String source = Files.readString(Path.of("../samples/JsonScrollPanel/scripts/client/populate_panel.js"));
        var host = new Host();
        ClientJavaScript.INSTANCE.execute(Scripts.Side.CLIENT,source,"populatePanel",host);
        assertEquals("Oak planks  |  Qty: 8",host.values.get("set_text:supply_row_1"),host.values.toString());
        assertEquals("false",host.values.get("set_visible:supply_row_16"));
        assertTrue(host.values.get("set_text:status").startsWith("Loaded 12 items"));
        host.values.put("panel_json","{invalid");
        ClientJavaScript.INSTANCE.execute(Scripts.Side.CLIENT,source,"populatePanel",host);
        assertEquals("Invalid JSON: previous list kept",host.values.get("set_text:status"));
        assertEquals("Oak planks  |  Qty: 8",host.values.get("set_text:supply_row_1"),host.values.toString());
    }
    @Test void noJavaAccessAndLoopBudget() {
        var host = new Host();
        ClientJavaScript.INSTANCE.execute(Scripts.Side.CLIENT,"if(typeof Packages!=='undefined'||typeof Java!=='undefined'||typeof java!=='undefined') throw Error('Java exposed');", "",host);
        assertThrows(IllegalStateException.class,()->ClientJavaScript.INSTANCE.execute(Scripts.Side.CLIENT,"while(true) {}","",host));
        assertThrows(Exception.class,()->ClientJavaScript.INSTANCE.execute(Scripts.Side.CLIENT,"ui.setText('a','partial'); throw Error('fail');","",host));
        assertFalse(host.values.containsKey("set_text:a"));
    }
}
