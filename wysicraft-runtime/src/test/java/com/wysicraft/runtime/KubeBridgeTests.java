package com.wysicraft.runtime;


import dev.latvian.mods.rhino.*;
import org.junit.jupiter.api.Test;
import java.nio.file.*;
import java.util.*;
import java.util.function.BiConsumer;
import static org.junit.jupiter.api.Assertions.*;

public class KubeBridgeTests {
    // Exercise the generated JS using the same Rhino release family as KubeJS 2101.
    // The Minecraft endpoints are doubles; this is not a live server test.
    public static class TestApi {
        public static BiConsumer<TestContext,String> callback;
        public static String text, command;
        public static void clearKubeHandlers(String id) { callback = null; }
        public static void registerKubeHandler(String id, String key, BiConsumer<TestContext,String> fn) { callback = fn; }
        public static void setVariable(TestPlayer player, String name, String value) { player.state.put(name,value); }
        public static void setText(TestPlayer player, String id, String value) { text = value; }
        public static void message(TestPlayer player, String value) { }
        public static int runCommand(TestPlayer player, String value) { command = value; return 1; }
    }
    public static class TestPlayer {
        final Map<String,String> state = new HashMap<>();
        public Object getServer() { return this; }
    }
    public record TestContext(TestPlayer player, Map<String,String> state) { }
    @Test void generatedScriptRegistersAndExecutesThroughJavaCallback() throws Exception {
        String source;
        try (var zip = new java.util.zip.ZipFile("../samples/kube_bridge-kubejs.zip")) {
            source = new String(zip.getInputStream(zip.getEntry("kubejs/server_scripts/wysicraft/kube_bridge.js")).readAllBytes(), java.nio.charset.StandardCharsets.UTF_8);
        }
        var cx = new ContextFactory().enter(); var scope = cx.initStandardObjects();
        ScriptableObject.putProperty(scope,"TestApi",new NativeJavaClass(cx,scope,TestApi.class),cx);
        cx.evaluateString(scope,"const Java = {loadClass: name => TestApi}; const global = {}; const console = {info: text => {}};", "bootstrap",1,null);
        cx.evaluateString(scope,source,"kube_bridge.js",1,null);
        assertNotNull(TestApi.callback);
        var player = new TestPlayer(); var context = new TestContext(player,player.state);
        TestApi.callback.accept(context,"");
        assertEquals("Server clicks: 1",TestApi.text);
        assertEquals("me tested the Wysicraft bridge",TestApi.command);
        cx.evaluateString(scope,source,"reload",1,null);
        TestApi.callback.accept(context,"");
        assertEquals("Server clicks: 2",TestApi.text);
    }
}
