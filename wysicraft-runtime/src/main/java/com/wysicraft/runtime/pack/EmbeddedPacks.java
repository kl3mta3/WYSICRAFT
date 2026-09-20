package com.wysicraft.runtime.pack;
import java.nio.file.*;
import java.util.*;
import net.neoforged.fml.ModList;
import com.wysicraft.runtime.Wysicraft;
public final class EmbeddedPacks {
    public static List<Path> paths() {
        var result=new ArrayList<Path>();
        for(var mod:ModList.get().getModFiles()) {
            Path directory=mod.getFile().findResource("wysicraft");
            if(Files.isDirectory(directory)) try(var files=Files.list(directory)) { files.filter(p->p.toString().endsWith(".wysicraft")).forEach(result::add); }
            catch(Exception ex) { Wysicraft.LOG.warn("Cannot read embedded packs: {}",ex.toString()); }
        }
        return result;
    }
}
