﻿using System;
using Eto.Forms;

namespace CelesteStudio.Mac;

class Program {
    [STAThread]
    public static void Main(string[] args) {
        new Application(Eto.Platforms.macOS).Run(new Studio());
    }
}