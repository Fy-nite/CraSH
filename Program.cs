using System;
using Adamantite.VFS;
using StarChart.AppFramework;
using StarChart.PTY;

namespace CraSH
{
	internal static class Program
	{
		static void Main(string[] args)
		{
			// Setup a simple in-memory VFS and mount it at root
			var memFs = new InMemoryFileSystem();
			memFs.CreateDirectory("");
			memFs.WriteAllBytes("welcome.txt", System.Text.Encoding.UTF8.GetBytes("Welcome to CraSH!\n"));

			var vfsManager = new VfsManager();
			vfsManager.Mount("/", memFs);
			VFSGlobal.Manager = vfsManager;

			var pty = new ConsolePty();
			var crashApp = new CraSHTerminalApp(pty, vfsManager);

			var ctx = new StarChart.Plugins.PluginContext { VFS = vfsManager, Arguments = args, WorkingDirectory = "/", PrimaryPty = pty };

			// Run using AppHost (will return when the shell exits)
			StarChart.AppFramework.AppHost.Run(crashApp, ctx);

			// exit process when shell returns
			return;
		}
	}
}
