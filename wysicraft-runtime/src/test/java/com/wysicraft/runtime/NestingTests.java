package com.wysicraft.runtime;
import com.wysicraft.runtime.model.*;
import com.wysicraft.runtime.pack.PackRepository;
import org.junit.jupiter.api.Test;
import java.nio.file.Path;
import java.util.Map;
import static org.junit.jupiter.api.Assertions.*;
class NestingTests {
    @Test void designerExportHasNestedContainersAndTrustedRowActions() throws Exception {
        var pack=PackRepository.load(Path.of("../artifacts/nesting-demo.wysicraft"));
        var ui=pack.screens().get("main");var list=ui.element("catalog");
        assertEquals(2,ContainerTree.ancestors(ui,list).size());assertEquals(6,list.rowElements.size());
        var item=RowTemplates.bind(list.rowElements.get(2),ItemRows.parse(list.value).getFirst(),0);
        assertEquals("minecraft:diamond",item.item);
        assertTrue(ItemRows.validEvent(list,"item_primary","0"));assertFalse(ItemRows.validEvent(list,"item_primary","5"));
        assertFalse(ItemRows.validEvent(list,"item_secondary","0"));
        list.rowElements.get(1).enabled=false;assertFalse(RowTemplates.hasAction(list,"item_primary",Map.of()));
        ui.element("outer").parent="inner";assertThrows(IllegalArgumentException.class,()->ContainerTree.ancestors(ui,list));
    }
}

