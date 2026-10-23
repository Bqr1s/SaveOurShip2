using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace SaveOurShip2;

// Runtime code analyzer that finds recursive method calls
static class RecusrionChecker
{
	private static readonly OpCode[] OneByteOpCodes = new OpCode[256];
	private static readonly OpCode[] TwoByteOpCodes = new OpCode[256];

	static RecusrionChecker()
	{
		foreach (FieldInfo field in typeof(OpCodes).GetFields(
					 BindingFlags.Public | BindingFlags.Static))
		{
			if (field.GetValue(null) is not OpCode opcode)
				continue;

			ushort value = unchecked((ushort)opcode.Value);

			if (value < 0x100)
				OneByteOpCodes[value] = opcode;
			else if ((value & 0xFF00) == 0xFE00)
				TwoByteOpCodes[value & 0xFF] = opcode;
		}
	}

	public static IEnumerable<MethodInfo> FindDirectRecursiveMethods(
		Assembly assembly)
	{
		foreach (Type type in assembly.GetTypes())
		{
			foreach (MethodInfo method in type.GetMethods(
						 BindingFlags.Public | BindingFlags.NonPublic |
						 BindingFlags.Instance | BindingFlags.Static |
						 BindingFlags.DeclaredOnly))
			{
				if (CallsItself(method))
					yield return method;
			}
		}
	}

	private static bool CallsItself(MethodInfo method)
	{
#nullable enable
		byte[]? il = method.GetMethodBody()?.GetILAsByteArray();
		if (il is null)
			return false;

		Type[] typeArgs = method.DeclaringType?.GetGenericArguments()
						  ?? Type.EmptyTypes;
		Type[] methodArgs = method.IsGenericMethod
							? method.GetGenericArguments()
							: Type.EmptyTypes;

		int position = 0;

		while (position < il.Length)
		{
			byte first = il[position++];
			OpCode opcode;

			if (first == 0xFE)
			{
				if (position >= il.Length)
					break;

				opcode = TwoByteOpCodes[il[position++]];
			}
			else
			{
				opcode = OneByteOpCodes[first];
			}

			if (opcode.Size == 0)
				break;

			if (opcode.OperandType is OperandType.InlineMethod)
			{
				if (position + 4 > il.Length)
					break;

				int token = BitConverter.ToInt32(il, position);

				try
				{
					MethodBase? called = method.Module.ResolveMethod(
						token, typeArgs, methodArgs);

					if (called is MethodInfo calledMethod &&
						SameMethod(method, calledMethod))
						return true;
				}
				catch (ArgumentException)
				{
					// The token could not be resolved in this generic context.
				}
			}

			int operandSize = GetOperandSize(opcode.OperandType, il, position);
			if (operandSize < 0 || position + operandSize > il.Length)
				break;

			position += operandSize;
		}

		return false;
#nullable disable
	}

	private static bool SameMethod(MethodInfo a, MethodInfo b)
	{
		if (a.IsGenericMethod)
			a = a.GetGenericMethodDefinition();

		if (b.IsGenericMethod)
			b = b.GetGenericMethodDefinition();

		return a.Module == b.Module &&
			   a.MetadataToken == b.MetadataToken;
	}

	private static int GetOperandSize(
		OperandType operandType, byte[] il, int position)
	{
		return operandType switch
		{
			OperandType.InlineNone => 0,
			OperandType.ShortInlineBrTarget or
			OperandType.ShortInlineI or
			OperandType.ShortInlineVar => 1,
			OperandType.InlineVar => 2,
			OperandType.InlineI or
			OperandType.InlineBrTarget or
			OperandType.InlineField or
			OperandType.InlineMethod or
			OperandType.InlineSig or
			OperandType.InlineString or
			OperandType.InlineTok or
			OperandType.InlineType or
			OperandType.ShortInlineR => 4,
			OperandType.InlineI8 or
			OperandType.InlineR => 8,
			OperandType.InlineSwitch => GetSwitchSize(il, position),
			_ => -1
		};
	}

	private static int GetSwitchSize(byte[] il, int position)
	{
		if (position + 4 > il.Length)
			return -1;

		int count = BitConverter.ToInt32(il, position);

		if (count < 0 || count > (il.Length - position - 4) / 4)
			return -1;

		return 4 + count * 4;
	}
}
