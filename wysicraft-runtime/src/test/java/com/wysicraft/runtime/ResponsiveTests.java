package com.wysicraft.runtime;
import com.wysicraft.runtime.model.*;
import com.wysicraft.runtime.model.Models.*;
import org.junit.jupiter.api.Test;
import static org.junit.jupiter.api.Assertions.*;
class ResponsiveTests {
    @Test void anchorsResizeWithoutDriftAndRowsFollowTheirList() {
        var design=new Ui();design.responsive=true;design.size.width=320;design.size.height=200;
        var panel=new Element();panel.id="panel";panel.type="panel";panel.horizontalAnchor="stretch";panel.verticalAnchor="stretch";panel.bounds.x=10;panel.bounds.y=10;panel.bounds.width=300;panel.bounds.height=180;panel.minWidth=120;panel.minHeight=80;
        var child=new Element();child.id="button";child.parent="panel";child.horizontalAnchor="right";child.verticalAnchor="bottom";child.bounds.x=250;child.bounds.y=160;
        var list=new Element();list.id="list";list.horizontalAnchor="stretch";list.bounds.width=300;list.rowTemplateWidth=300;
        var row=new Element();row.id="row";row.horizontalAnchor="right";row.bounds.x=240;list.rowElements.add(row);
        design.elements.add(panel);design.elements.add(child);design.elements.add(list);var target=design.copy();
        ResponsiveLayout.apply(target,design,520,300);assertEquals(500,target.element("panel").bounds.width);assertEquals(450,target.element("button").bounds.x);assertEquals(260,target.element("button").bounds.y);assertEquals(440,target.element("list").rowElements.getFirst().bounds.x);
        ResponsiveLayout.apply(target,design,40,40);assertEquals(120,target.element("panel").bounds.width);assertEquals(80,target.element("panel").bounds.height);
        ResponsiveLayout.apply(target,design,320,200);assertEquals(250,target.element("button").bounds.x);
        design.responsive=false;ResponsiveLayout.apply(target,design,800,600);assertEquals(320,target.size.width);assertEquals(250,target.element("button").bounds.x);
    }
}
