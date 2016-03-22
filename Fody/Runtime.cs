using System;

public static class Runtime
{
	public static bool IsMono()
	{
		return Type.GetType("Mono.Runtime") != null;
	}
}

