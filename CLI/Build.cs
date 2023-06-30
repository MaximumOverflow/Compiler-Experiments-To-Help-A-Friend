using System.Diagnostics;
using Squyrm.Utilities;
using Squyrm.Compiler;
using CommandLine;
using LLVMSharp.Interop;
using Pastel;

namespace Squyrm.CLI;

[Verb("build", HelpText = "Build a Squyrm executable or library.")]
public sealed class BuildOptions
{
	[Value(0, MetaName = "path", HelpText = "The path to the file or project to compile.")]
	public required string Path { get; init; }
	
	[Option("module-name", HelpText = "The name of the resulting LLVM module.")]
	public string? ModuleName { get; init; }
	
	[Option('O', HelpText = "The level of optimization to apply.")]
	public uint OptimizationLevel { get; init; }
	
	[Option("emit-reflection-metadata", Default = true, HelpText = "Include reflection metadata in the resulting binary. Programs requiring reflection will fail to compile if this flag is not set.")]
	public bool EmitReflectionMetadata { get; init; }
	
	[Option('f', "output-format", Default = OutputFormat.LlvmIR)]
	public OutputFormat OutputFormat { get; init; }
}

public enum OutputFormat
{
	Asm,
	LlvmIR,
	StaticLib,
	DynamicLib,
	Executable,
}

public static class Build
{
	public static unsafe int Execute(BuildOptions options)
	{
		if (!Path.Exists(options.Path))
		{
			Console.Error.WriteLine("Path not found.".Pastel(ConsoleColor.Red));
			return 1;
		}


		IEnumerable<string> files;
		if ((File.GetAttributes(options.Path) & FileAttributes.Directory) != 0)
		{
			files = Directory.EnumerateFiles(options.Path, "*.sqm", SearchOption.AllDirectories);
		}
		else if(Path.GetExtension(options.Path) != ".sqm")
		{
			Console.Error.WriteLine("File is not a Squyrm file.".Pastel(ConsoleColor.Red));
			return 1;
		}
		else
		{
			files = new[] { options.Path };
		}
		
		using var context = new CompilationContext(new CompilationSettings
		{
			ModuleName = options.ModuleName ?? "Unnamed module",
			OptimizationLevel = options.OptimizationLevel,
			EmitReflectionInformation = options.EmitReflectionMetadata,
		}, files);

		var allStats = new RuntimeStats();

		LLVMModuleRef llvmModule;
		try
		{
			llvmModule = context.Compile();
		}
		catch (CompilationException e)
		{
			e.Dump(ConsoleColor.Red);
			return 1;
		}

		string dir;
		if ((File.GetAttributes(options.Path) & FileAttributes.Directory) != 0)
		{
			var directory = new DirectoryInfo(options.Path);
			directory = directory.Parent ?? directory;
			dir = directory.FullName;
		}
		else
		{
			var directory = new FileInfo(options.Path).Directory ?? throw new DirectoryNotFoundException();
			dir = directory.FullName;
		}
		
		switch (options.OutputFormat)
		{
			case OutputFormat.Asm:
			{
				var path = Path.Join(dir, "out.asm");
				var cpu = new string(LLVM.GetHostCPUName());
				var features = new string(LLVM.GetHostCPUFeatures());

				var optLevel = (LLVMCodeGenOptLevel) options.OptimizationLevel;
				var target = LLVMTargetRef.GetTargetFromTriple(LLVMTargetRef.DefaultTriple);
				var machine = target.CreateTargetMachine(LLVMTargetRef.DefaultTriple, cpu, features,
					optLevel, LLVMRelocMode.LLVMRelocDefault,
					LLVMCodeModel.LLVMCodeModelDefault);

				machine.EmitToFile(llvmModule, path, LLVMCodeGenFileType.LLVMAssemblyFile);
				break;
			}
			
			case OutputFormat.LlvmIR:
			{
				var path = Path.Join(dir, "out.ll");
				llvmModule.PrintToFile(path);
				break;
			}

			case OutputFormat.Executable:
			{
				var path = Path.Join(dir, "out.o");
				var cpu = new string(LLVM.GetHostCPUName());
				var features = new string(LLVM.GetHostCPUFeatures());

				var optLevel = (LLVMCodeGenOptLevel) options.OptimizationLevel;
				var target = LLVMTargetRef.GetTargetFromTriple(LLVMTargetRef.DefaultTriple);
				var machine = target.CreateTargetMachine(LLVMTargetRef.DefaultTriple, cpu, features,
					optLevel, LLVMRelocMode.LLVMRelocDefault,
					LLVMCodeModel.LLVMCodeModelDefault);

				machine.EmitToFile(llvmModule, path, LLVMCodeGenFileType.LLVMObjectFile);
				var clang = Process.Start(new ProcessStartInfo
				{
					FileName = "clang",
					WorkingDirectory = dir,
					Arguments = "out.o -o out.exe",
				});
				
				if (clang is null)
				{
					Console.WriteLine("'clang' executable not found.".Pastel(ConsoleColor.Red));
					return 1;
				}
				
				clang.WaitForExit();
				File.Delete(path);
				break;
			}
			
			default: 
				throw new NotImplementedException();
		}
		
		allStats.Dump("Compilation", ConsoleColor.Green);
		return 0;
	}
}