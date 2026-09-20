package com.wysicraft.runtime.api;

import dev.latvian.mods.kubejs.plugin.KubeJSPlugin;
import dev.latvian.mods.kubejs.script.ScriptManager;
import dev.latvian.mods.kubejs.script.ScriptType;
import dev.latvian.mods.kubejs.script.ScriptPack;
import dev.latvian.mods.kubejs.script.ScriptPackInfo;
import dev.latvian.mods.kubejs.script.ScriptFile;
import net.neoforged.fml.ModList;
import java.nio.file.Files;

/** KubeJS discovers this plugin only when KubeJS is installed. No files are extracted. */
public final class BundledKubePlugin implements KubeJSPlugin {
    @Override public void beforeScriptsLoaded(ScriptManager manager) {
        if (manager.scriptType == ScriptType.CLIENT) return;
        String side = manager.scriptType == ScriptType.STARTUP ? "startup_scripts" : "server_scripts";
        for (var mod : ModList.get().getModFiles()) {
            var path = mod.getFile().findResource("wysicraft_kube", side);
            if (!Files.isDirectory(path)) continue;
            String namespace = "wysicraft_" + mod.getMods().getFirst().getModId();
            var pack = new ScriptPack(manager, new ScriptPackInfo(namespace, ""));
            manager.collectScripts(pack, path, "");
            for (var info : pack.info.scripts) try {
                var file = new ScriptFile(pack, info);
                if (file.skipLoading().isEmpty()) pack.scripts.add(file);
            } catch (Exception ex) { throw new IllegalStateException("Cannot load bundled KubeJS script " + info.location, ex); }
            manager.packs.put(namespace, pack);
        }
    }
}
