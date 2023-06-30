namespace Squyrm.Compiler.Compiler.Passes;

internal static class IROptimizationPass
{
	internal static unsafe void Execute(CompilationContext context, uint optLevel)
	{
		if(optLevel == 0) return;
		
		using LLVMPassManagerBuilderRef passManagerBuilder = LLVM.PassManagerBuilderCreate();
		LLVM.PassManagerBuilderSetOptLevel(passManagerBuilder, optLevel);
			
		using LLVMPassManagerRef modulePassManager = LLVM.CreatePassManager();
		passManagerBuilder.PopulateModulePassManager(modulePassManager);
			
		using var functionPassManager = context.LlvmModule.CreateFunctionPassManager();
		passManagerBuilder.PopulateFunctionPassManager(functionPassManager);
		functionPassManager.InitializeFunctionPassManager();
			
		modulePassManager.Run(context.LlvmModule);
		
		foreach (var (_, ns) in context.Namespaces)
		foreach (var (_, fn) in ns.Functions)
		{
			if (fn.External) continue;
			functionPassManager.RunFunctionPassManager(fn);
		}
		
		modulePassManager.Run(context.LlvmModule);
	}
}