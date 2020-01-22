// Copyright 2000-2017 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using JetBrainsDecompiler.Struct.Consts;
using JetBrainsDecompiler.Util;
using Sharpen;

namespace JetBrainsDecompiler.Struct.Attr
{
	/// <summary>
	/// u2 line_number_table_length;
	/// {  u2 start_pc;
	/// u2 line_number;
	/// } line_number_table[line_number_table_length];
	/// Created by Egor on 05.10.2014.
	/// </summary>
	public class StructLineNumberTableAttribute : StructGeneralAttribute
	{
		private int[] myLineInfo = InterpreterUtil.Empty_Int_Array;

		/// <exception cref="System.IO.IOException"/>
		public override void InitContent(DataInputFullStream data, ConstantPool pool)
		{
			int len = data.ReadUnsignedShort() * 2;
			if (len > 0)
			{
				myLineInfo = new int[len];
				for (int i = 0; i < len; i += 2)
				{
					myLineInfo[i] = data.ReadUnsignedShort();
					myLineInfo[i + 1] = data.ReadUnsignedShort();
				}
			}
			else if (myLineInfo.Length > 0)
			{
				myLineInfo = InterpreterUtil.Empty_Int_Array;
			}
		}

		public virtual int FindLineNumber(int pc)
		{
			if (myLineInfo.Length >= 2)
			{
				for (int i = myLineInfo.Length - 2; i >= 0; i -= 2)
				{
					if (pc >= myLineInfo[i])
					{
						return myLineInfo[i + 1];
					}
				}
			}
			return -1;
		}

		public virtual int[] GetRawData()
		{
			return myLineInfo;
		}
	}
}
