using System.Text;
using Pastel;

namespace Squyrm.Utilities;

public readonly struct RuntimeStats
{
	public readonly long StartBytes;
	public readonly DateTime StartTime;
	public readonly TimeSpan StartGCTime;
	public readonly (int, int, int) StartGCCount;

	public RuntimeStats()
	{
		StartBytes = GC.GetTotalAllocatedBytes(true);
		StartGCTime = GC.GetTotalPauseDuration();
		StartGCCount = (GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2));
		StartTime = DateTime.Now;
	}

	public TimeSpan TotalElapsedTime => DateTime.Now - StartTime;
	public string TotalElapsedTimeString => GetTimeString(TotalElapsedTime);

	public TimeSpan TotalGCTime => GC.GetTotalPauseDuration() - StartGCTime;
	public string TotalGCTimeString => GetTimeString(TotalGCTime);

	public (int, int, int) TotalGCCollectionEvents => (
		GC.CollectionCount(0) - StartGCCount.Item1,
		GC.CollectionCount(1) - StartGCCount.Item2,
		GC.CollectionCount(2) - StartGCCount.Item3
	);

	public long TotalAllocatedMemory => GC.GetTotalAllocatedBytes(true) - StartBytes;
	
	public string TotalAllocatedMemoryString
	{
		get
		{
			var totalAllocated = TotalAllocatedMemory;
			return totalAllocated switch
			{
				> 1000000000 => $"{totalAllocated / 1000000000}GB",
				> 1000000 => $"{totalAllocated / 1000000}MB",
				> 1000 => $"{totalAllocated / 1000}KB",
				_ => $"{totalAllocated}B",
			};
		}
	}

	public void Dump(string operationName, ConsoleColor color)
	{
		var collections = TotalGCCollectionEvents;
		var allocated = TotalAllocatedMemoryString;
		var time = TotalGCTimeString;
		var elapsed = TotalElapsedTimeString;

		var builder = new StringBuilder(operationName);
		builder.Append(" completed in ");
		builder.Append(elapsed);
		builder.Append(". Total bytes allocated: ");
		builder.Append(allocated);
		builder.Append(". Time spent in GC: ");
		builder.Append(time);
		builder.Append(". GC pauses: ");
		builder.Append(collections.Item1);
		builder.Append(", ");
		builder.Append(collections.Item2);
		builder.Append(", ");
		builder.Append(collections.Item3);
		builder.Append('.');
		
		Console.WriteLine(builder.ToString().Pastel(color));
	}
	
	private static string GetTimeString(TimeSpan duration)
	{
		if (duration.TotalMinutes >= 1) return $"{duration.TotalMinutes}m";
		if (duration.TotalSeconds >= 1) return $"{duration.TotalSeconds}s";
		if (duration.TotalMilliseconds >= 1) return $"{duration.TotalMilliseconds}ms";
		return $"{duration.TotalMicroseconds}us";
	}
}