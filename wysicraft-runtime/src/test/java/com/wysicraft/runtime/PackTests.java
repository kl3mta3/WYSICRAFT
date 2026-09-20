package com.wysicraft.runtime;

import com.wysicraft.runtime.model.Expressions;
import com.wysicraft.runtime.pack.PackRepository;
import org.junit.jupiter.api.Test;
import java.nio.file.Path;
import java.util.Map;
import static org.junit.jupiter.api.Assertions.*;

class PackTests {
    @Test void projectsCanBothHaveMain() throws Exception {
        Path root=java.nio.file.Files.createTempDirectory("wysicraft-scope");
        for(String id:new String[]{"one","two"}) {
            Path folder=root.resolve("dev").resolve(id);java.nio.file.Files.createDirectories(folder.resolve("ui"));
            java.nio.file.Files.writeString(folder.resolve("project.json"),"{\"id\":\""+id+"\",\"defaultUi\":\"main\",\"ui\":[\"main\"]}");
            java.nio.file.Files.writeString(folder.resolve("ui/main.json"),"{\"id\":\"main\",\"events\":{\"open\":{\"client\":{\"actions\":[{\"type\":\"open_ui\",\"value\":\"main\"}]}}}}");
        }
        var repository=new PackRepository();repository.reload(root,(m,e)->{if(e!=null)throw new AssertionError(e);});
        assertEquals(2,repository.all().size());assertNull(repository.get("main"));
        assertEquals("one:main",repository.get("one:main").id);
        assertEquals("two:main",repository.get("two:main").events.get("open").client.actions.getFirst().value);
    }
    @Test void canonicalAndLegacyTexturesResolveWithoutNamespaceCollision() {
        var manifest = new com.wysicraft.runtime.model.Models.Manifest(); manifest.id = "sample";
        byte[] bytes = new byte[]{1,2,3};
        var canonical = new PackRepository.Loaded(manifest,Map.of(),Map.of("assets/sample/textures/gui/image/background.png",bytes));
        assertArrayEquals(bytes,PackRepository.textureFile(canonical,"sample:textures/gui/image/background.png"));
        assertArrayEquals(bytes,PackRepository.textureFile(canonical,"sample:background.png"));
        assertNull(PackRepository.textureFile(canonical,"other:background.png"));
        var legacy = new PackRepository.Loaded(manifest,Map.of(),Map.of("assets/textures/background.png",bytes));
        assertArrayEquals(bytes,PackRepository.textureFile(legacy,"sample:textures/gui/image/background.png"));
    }
    @Test void designerPackLoadsInJava() throws Exception {
        var pack = PackRepository.load(Path.of("../samples/airship_controls.wysicraft"));
        var start = pack.screens().get("cockpit").element("engine_start");
        assertEquals("say Engine start requested",start.events.get("click").server.actions.getFirst().value);
        assertEquals("set_text",start.events.get("click").client.actions.getFirst().type);
        assertEquals(2,pack.screens().size());
    }
    @Test void unsafePathsAndFutureVersions() {
        for (String path : new String[]{"../server.properties","assets/../../x","C:/secret","/root","assets\\x"}) assertThrows(IllegalArgumentException.class,() -> PackRepository.safePath(path));
        assertTrue(PackRepository.compareVersion("2.0.0","1.0.0") > 0);
        assertThrows(IllegalArgumentException.class,() -> PackRepository.compareVersion("banana","1.0.0"));
    }
    @Test void safeConditionsAndBindings() {
        var state = Map.of("altitude","20","engineRunning","true");
        assertTrue(Expressions.evaluate("altitude > 10 AND NOT (engineRunning == false)",state));
        assertFalse(Expressions.evaluate("altitude < 10 OR engineRunning != true",state));
        assertEquals("Altitude: 20",Expressions.bind("Altitude: ${altitude}",state));
        assertThrows(IllegalArgumentException.class,() -> Expressions.evaluate("Runtime.exec('x')",state));
    }
}
