package com.wysicraft.runtime.model;

import java.util.*;
import com.wysicraft.runtime.pack.PackRepository;

/** Bounded display data, never an instruction to grant/move inventory items. */
public final class ItemRows {
    public record Row(String item,int count,String name) {}
    public static boolean validEvent(Models.Element e,String event,String value) { try { if(!e.rowElements.isEmpty()) {if(!event.equals("item_click") && !com.wysicraft.runtime.model.RowTemplates.hasAction(e,event))return false;} else if(event.equals("item_primary") && e.primaryLabel.isEmpty() || event.equals("item_secondary") && e.secondaryLabel.isEmpty()) return false; int index=Integer.parseInt(value); return index>=0 && index<com.wysicraft.runtime.model.ItemRows.parse(e.value).size(); } catch(Exception ex) { return false; } }

    public static List<Row> parse(String json) {
        if (json==null || json.length()>4096) throw new IllegalArgumentException("Item list exceeds 4096 characters");
        var rows=Models.JSON.fromJson(json,Row[].class);
        if(rows==null || rows.length>128) throw new IllegalArgumentException("Expected up to 128 item rows");
        for(var row:rows) if(row==null || !PackRepository.resource(row.item) || row.count<0 || row.name==null || row.name.length()>80) throw new IllegalArgumentException("Rows require item ID, nonnegative count and name (up to 80 characters)");
        return List.of(rows);
    }
}

