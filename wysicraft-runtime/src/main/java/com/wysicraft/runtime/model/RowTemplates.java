package com.wysicraft.runtime.model;

import java.util.*;
import com.wysicraft.runtime.model.Models.*;

public final class RowTemplates {
    public static final Set<String> TYPES=Set.of("panel","label","image","texture_region","item","button","progress");
    public static final Set<String> ACTIONS=Set.of("","item_click","item_primary","item_secondary");
    public static Ui layout(Element list) { var ui=new Ui(); ui.elements=list.rowElements; return ui; }
    public static void check(Element list) {
        if(list.rowElements.size()>64)throw new IllegalArgumentException("Row template exceeds 64 elements");
        var ui=layout(list);
        for(var e:list.rowElements) {
            if(!TYPES.contains(e.type) || !e.rowTemplate.isEmpty() || !e.rowElements.isEmpty())throw new IllegalArgumentException("Unsupported or recursive row template control");
            if(!ACTIONS.contains(e.rowAction) || !e.rowAction.isEmpty() && !e.type.equals("button"))throw new IllegalArgumentException("Invalid row action");
            if(e.events.values().stream().anyMatch(v->!v.client.actions.isEmpty() || !v.server.actions.isEmpty() || !v.client.script.isEmpty() || !v.server.script.isEmpty()))throw new IllegalArgumentException("Row events belong on the owning list");
            ContainerTree.ancestors(ui,e);
        }
    }
    public static boolean hasAction(Element list,String event) {
        return list.rowElements.stream().anyMatch(e->e.visible && e.enabled && e.rowAction.equals(event)
            && ContainerTree.ancestors(layout(list),e).stream().allMatch(p->p.visible && p.enabled));
    }
    public static boolean hasAction(Element list,String event,Map<String,String> state) {
        return list.rowElements.stream().anyMatch(e->e.rowAction.equals(event) && e.visible && e.enabled && Expressions.evaluate(e.visibleIf,state) && Expressions.evaluate(e.enabledIf,state) && ContainerTree.ancestors(layout(list),e).stream().allMatch(p->p.visible && p.enabled && Expressions.evaluate(p.visibleIf,state) && Expressions.evaluate(p.enabledIf,state)));
    }
    public static Element bind(Element source,ItemRows.Row row,int index) {
        var e=Models.JSON.fromJson(Models.JSON.toJson(source),Element.class);
        e.text=bind(e.text,row,index);e.item=bind(e.item,row,index);e.value=bind(e.value,row,index);e.tooltip=bind(e.tooltip,row,index);return e;
    }
    private static String bind(String s,ItemRows.Row row,int index) {
        return s.replace("${row.item}",row.item()).replace("${row.name}",row.name()).replace("${row.count}",Integer.toString(row.count())).replace("${row.index}",Integer.toString(index));
    }
}

