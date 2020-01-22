// Copyright 2000-2018 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System.Collections.Generic;
using JetBrainsDecompiler.Code;
using JetBrainsDecompiler.Main;
using JetBrainsDecompiler.Main.Collectors;
using JetBrainsDecompiler.Main.Extern;
using JetBrainsDecompiler.Main.Rels;
using JetBrainsDecompiler.Modules.Decompiler;
using JetBrainsDecompiler.Modules.Decompiler.Vars;
using JetBrainsDecompiler.Struct;
using JetBrainsDecompiler.Struct.Attr;
using JetBrainsDecompiler.Struct.Gen;
using JetBrainsDecompiler.Struct.Gen.Generics;
using JetBrainsDecompiler.Struct.Match;
using JetBrainsDecompiler.Util;
using Sharpen;

namespace JetBrainsDecompiler.Modules.Decompiler.Exps
{
	public class VarExprent : Exprent
	{
		public const int Stack_Base = 10000;

		public const string Var_Nameless_Enclosure = "<VAR_NAMELESS_ENCLOSURE>";

		private int index;

		private VarType varType;

		private bool definition = false;

		private readonly VarProcessor processor;

		private readonly int visibleOffset;

		private int version = 0;

		private bool classDef = false;

		private bool stack = false;

		public VarExprent(int index, VarType varType, VarProcessor processor)
			: this(index, varType, processor, -1)
		{
		}

		public VarExprent(int index, VarType varType, VarProcessor processor, int visibleOffset
			)
			: base(Exprent_Var)
		{
			this.index = index;
			this.varType = varType;
			this.processor = processor;
			this.visibleOffset = visibleOffset;
		}

		public override VarType GetExprType()
		{
			return GetVarType();
		}

		public override int GetExprentUse()
		{
			return Exprent.Multiple_Uses | Exprent.Side_Effects_Free;
		}

		public override List<Exprent> GetAllExprents()
		{
			return new List<Exprent>();
		}

		public override Exprent Copy()
		{
			VarExprent var = new VarExprent(index, GetVarType(), processor, visibleOffset);
			var.SetDefinition(definition);
			var.SetVersion(version);
			var.SetClassDef(classDef);
			var.SetStack(stack);
			return var;
		}

		public override TextBuffer ToJava(int indent, BytecodeMappingTracer tracer)
		{
			TextBuffer buffer = new TextBuffer();
			tracer.AddMapping(bytecode);
			if (classDef)
			{
				ClassesProcessor.ClassNode child = DecompilerContext.GetClassProcessor().GetMapRootClasses
					().GetOrNull(varType.value);
				new ClassWriter().ClassToJava(child, buffer, indent, tracer);
				tracer.IncrementCurrentSourceLine(buffer.CountLines());
			}
			else
			{
				VarVersionPair varVersion = GetVarVersionPair();
				string name = null;
				if (processor != null)
				{
					name = processor.GetVarName(varVersion);
				}
				if (definition)
				{
					if (processor != null && processor.GetVarFinal(varVersion) == VarTypeProcessor.Var_Explicit_Final)
					{
						buffer.Append("final ");
					}
					AppendDefinitionType(buffer);
					buffer.Append(" ");
				}
				buffer.Append(name == null ? ("var" + index + (this.version == 0 ? string.Empty : 
					"_" + this.version)) : name);
			}
			return buffer;
		}

		public virtual VarVersionPair GetVarVersionPair()
		{
			return new VarVersionPair(index, version);
		}

		public virtual string GetDebugName(StructMethod method)
		{
			StructLocalVariableTableAttribute attr = method.GetLocalVariableAttr();
			if (attr != null && processor != null)
			{
				int? origIndex = processor.GetVarOriginalIndex(index);
				if (origIndex != null)
				{
					string name = attr.GetName(origIndex.Value, visibleOffset);
					if (name != null && TextUtil.IsValidIdentifier(name, method.GetClassStruct().GetBytecodeVersion
						()))
					{
						return name;
					}
				}
			}
			return null;
		}

		private void AppendDefinitionType(TextBuffer buffer)
		{
			if (DecompilerContext.GetOption(IFernflowerPreferences.Use_Debug_Var_Names))
			{
				MethodWrapper method = (MethodWrapper)DecompilerContext.GetProperty(DecompilerContext
					.Current_Method_Wrapper);
				if (method != null)
				{
					int? originalIndex = null;
					if (processor != null)
					{
						originalIndex = processor.GetVarOriginalIndex(index);
					}
					if (originalIndex != null)
					{
						// first try from signature
						if (DecompilerContext.GetOption(IFernflowerPreferences.Decompile_Generic_Signatures
							))
						{
							StructLocalVariableTypeTableAttribute attr = method.methodStruct.GetAttribute(StructGeneralAttribute
								.Attribute_Local_Variable_Type_Table);
							if (attr != null)
							{
								string signature = attr.GetSignature(originalIndex, visibleOffset);
								if (signature != null)
								{
									GenericFieldDescriptor descriptor = GenericMain.ParseFieldSignature(signature);
									if (descriptor != null)
									{
										buffer.Append(GenericMain.GetGenericCastTypeName(descriptor.type));
										return;
									}
								}
							}
						}
						// then try from descriptor
						StructLocalVariableTableAttribute attr_1 = method.methodStruct.GetLocalVariableAttr
							();
						if (attr_1 != null)
						{
							string descriptor = attr_1.GetDescriptor(originalIndex, visibleOffset);
							if (descriptor != null)
							{
								buffer.Append(ExprProcessor.GetCastTypeName(new VarType(descriptor)));
								return;
							}
						}
					}
				}
			}
			buffer.Append(ExprProcessor.GetCastTypeName(GetVarType()));
		}

		public override bool Equals(object o)
		{
			if (o == this)
			{
				return true;
			}
			if (!(o is VarExprent))
			{
				return false;
			}
			VarExprent ve = (VarExprent)o;
			return index == ve.GetIndex() && version == ve.GetVersion() && InterpreterUtil.EqualObjects
				(GetVarType(), ve.GetVarType());
		}

		// FIXME: varType comparison redundant?
		public virtual int GetIndex()
		{
			return index;
		}

		public virtual void SetIndex(int index)
		{
			this.index = index;
		}

		public virtual VarType GetVarType()
		{
			VarType vt = null;
			if (processor != null)
			{
				vt = processor.GetVarType(GetVarVersionPair());
			}
			if (vt == null || (varType != null && varType.type != ICodeConstants.Type_Unknown
				))
			{
				vt = varType;
			}
			return vt == null ? VarType.Vartype_Unknown : vt;
		}

		public virtual void SetVarType(VarType varType)
		{
			this.varType = varType;
		}

		public virtual bool IsDefinition()
		{
			return definition;
		}

		public virtual void SetDefinition(bool definition)
		{
			this.definition = definition;
		}

		public virtual VarProcessor GetProcessor()
		{
			return processor;
		}

		public virtual int GetVersion()
		{
			return version;
		}

		public virtual void SetVersion(int version)
		{
			this.version = version;
		}

		public virtual bool IsClassDef()
		{
			return classDef;
		}

		public virtual void SetClassDef(bool classDef)
		{
			this.classDef = classDef;
		}

		public virtual bool IsStack()
		{
			return stack;
		}

		public virtual void SetStack(bool stack)
		{
			this.stack = stack;
		}

		// *****************************************************************************
		// IMatchable implementation
		// *****************************************************************************
		public override bool Match(MatchNode matchNode, MatchEngine engine)
		{
			if (!base.Match(matchNode, engine))
			{
				return false;
			}
			MatchNode.RuleValue rule = matchNode.GetRules().GetOrNull(IMatchable.MatchProperties
				.Exprent_Var_Index);
			if (rule != null)
			{
				if (rule.IsVariable())
				{
					return engine.CheckAndSetVariableValue((string)rule.value, this.index);
				}
				else
				{
					return this.index == int.Parse((string)rule.value);
				}
			}
			return true;
		}
	}
}
