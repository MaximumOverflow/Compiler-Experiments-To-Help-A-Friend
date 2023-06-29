namespace LanguageParser.Compiler;

public struct CompilationSettings
{
	private uint _optLevel;
	public required string ModuleName { get; set; }
	public bool EmitReflectionInformation { get; set; }

	public uint OptimizationLevel
	{
		get => _optLevel;
		set => _optLevel = Math.Clamp(value, 0, 3);
	}
}