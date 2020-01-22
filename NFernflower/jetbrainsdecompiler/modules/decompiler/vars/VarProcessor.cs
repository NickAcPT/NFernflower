// Copyright 2000-2017 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System.Collections;
using System.Collections.Generic;
using JetBrainsDecompiler.Main.Collectors;
using JetBrainsDecompiler.Modules.Decompiler.Stats;
using JetBrainsDecompiler.Struct;
using JetBrainsDecompiler.Struct.Gen;
using JetBrainsDecompiler.Util;
using Sharpen;

namespace JetBrainsDecompiler.Modules.Decompiler.Vars
{
	public class VarProcessor
	{
		private readonly VarNamesCollector varNamesCollector = new VarNamesCollector();

		private readonly StructMethod method;

		private readonly MethodDescriptor methodDescriptor;

		private IDictionary<VarVersionPair, string> mapVarNames = new Dictionary<VarVersionPair
			, string>();

		private VarVersionsProcessor varVersions;

		private readonly IDictionary<VarVersionPair, string> thisVars = new Dictionary<VarVersionPair
			, string>();

		private readonly HashSet<VarVersionPair> externalVars = new HashSet<VarVersionPair
			>();

		public VarProcessor(StructMethod mt, MethodDescriptor md)
		{
			method = mt;
			methodDescriptor = md;
		}

		public virtual void SetVarVersions(RootStatement root)
		{
			VarVersionsProcessor oldProcessor = varVersions;
			varVersions = new VarVersionsProcessor(method, methodDescriptor);
			varVersions.SetVarVersions(root, oldProcessor);
		}

		public virtual void SetVarDefinitions(Statement root)
		{
			mapVarNames = new Dictionary<VarVersionPair, string>();
			new VarDefinitionHelper(root, method, this).SetVarDefinitions();
		}

		public virtual void SetDebugVarNames(IDictionary<int, string> mapDebugVarNames)
		{
			if (varVersions == null)
			{
				return;
			}
			IDictionary<int, int> mapOriginalVarIndices = varVersions.GetMapOriginalVarIndices
				();
			List<VarVersionPair> listVars = new List<VarVersionPair>(mapVarNames.Keys);
			listVars.Sort(IComparer.ComparingInt((VarVersionPair o) => o.var));
			IDictionary<string, int> mapNames = new Dictionary<string, int>();
			foreach (VarVersionPair pair in listVars)
			{
				string name = mapVarNames.GetOrNull(pair);
				int? index = mapOriginalVarIndices.GetOrNullable(pair.var);
				if (index != null)
				{
					string debugName = mapDebugVarNames.GetOrNull(index);
					if (debugName != null && TextUtil.IsValidIdentifier(debugName, method.GetClassStruct
						().GetBytecodeVersion()))
					{
						name = debugName;
					}
				}
				int? counter = mapNames.GetOrNullable(name);
				Sharpen.Collections.Put(mapNames, name, counter == null ? counter.Value = 0 : ++counter
					.Value);
				if (counter.Value > 0)
				{
					name += counter.ToString();
				}
				Sharpen.Collections.Put(mapVarNames, pair, name);
			}
		}

		public virtual int GetVarOriginalIndex(int index)
		{
			if (varVersions == null)
			{
				return null;
			}
			return varVersions.GetMapOriginalVarIndices().GetOrNullable(index);
		}

		public virtual void RefreshVarNames(VarNamesCollector vc)
		{
			IDictionary<VarVersionPair, string> tempVarNames = new Dictionary<VarVersionPair, 
				string>(mapVarNames);
			foreach (KeyValuePair<VarVersionPair, string> ent in tempVarNames)
			{
				Sharpen.Collections.Put(mapVarNames, ent.Key, vc.GetFreeName(ent.Value));
			}
		}

		public virtual VarNamesCollector GetVarNamesCollector()
		{
			return varNamesCollector;
		}

		public virtual VarType GetVarType(VarVersionPair pair)
		{
			return varVersions == null ? null : varVersions.GetVarType(pair);
		}

		public virtual void SetVarType(VarVersionPair pair, VarType type)
		{
			varVersions.SetVarType(pair, type);
		}

		public virtual string GetVarName(VarVersionPair pair)
		{
			return mapVarNames == null ? null : mapVarNames.GetOrNull(pair);
		}

		public virtual void SetVarName(VarVersionPair pair, string name)
		{
			Sharpen.Collections.Put(mapVarNames, pair, name);
		}

		public virtual ICollection<string> GetVarNames()
		{
			return mapVarNames != null ? mapVarNames.Values : new System.Collections.Generic.HashSet<
				string>();
		}

		public virtual int GetVarFinal(VarVersionPair pair)
		{
			return varVersions == null ? VarTypeProcessor.Var_Final : varVersions.GetVarFinal
				(pair);
		}

		public virtual void SetVarFinal(VarVersionPair pair, int finalType)
		{
			varVersions.SetVarFinal(pair, finalType);
		}

		public virtual IDictionary<VarVersionPair, string> GetThisVars()
		{
			return thisVars;
		}

		public virtual HashSet<VarVersionPair> GetExternalVars()
		{
			return externalVars;
		}
	}
}
