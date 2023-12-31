﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Editor.Panels;

public interface IPanel
{

    public void Renderer(double DeltaTime);
}

[AttributeUsage(AttributeTargets.Class)]
public class AddPanelToEditorAttribute : Attribute
{

}