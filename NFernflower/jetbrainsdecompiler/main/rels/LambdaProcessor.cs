// Copyright 2000-2018 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using System.Collections.Generic;
using Java.Util;
using JetBrainsDecompiler.Code;
using JetBrainsDecompiler.Main;
using JetBrainsDecompiler.Struct;
using JetBrainsDecompiler.Struct.Attr;
using JetBrainsDecompiler.Struct.Consts;
using JetBrainsDecompiler.Struct.Gen;
using JetBrainsDecompiler.Util;
using Sharpen;

namespace JetBrainsDecompiler.Main.Rels
{
	public class LambdaProcessor
	{
		private const string Javac_Lambda_Class = "java/lang/invoke/LambdaMetafactory";

		private const string Javac_Lambda_Method = "metafactory";

		private const string Javac_Lambda_Alt_Method = "altMetafactory";

		/// <exception cref="System.IO.IOException"/>
		public virtual void ProcessClass(ClassesProcessor.ClassNode node)
		{
			foreach (ClassesProcessor.ClassNode child in node.nested)
			{
				ProcessClass(child);
			}
			ClassesProcessor clProcessor = DecompilerContext.GetClassProcessor();
			StructClass cl = node.classStruct;
			if (cl.GetBytecodeVersion() < ICodeConstants.Bytecode_Java_8)
			{
				// lambda beginning with Java 8
				return;
			}
			StructBootstrapMethodsAttribute bootstrap = cl.GetAttribute(StructGeneralAttribute
				.Attribute_Bootstrap_Methods);
			if (bootstrap == null || bootstrap.GetMethodsNumber() == 0)
			{
				return;
			}
			// no bootstrap constants in pool
			BitSet lambda_methods = new BitSet();
			// find lambda bootstrap constants
			for (int i = 0; i < bootstrap.GetMethodsNumber(); ++i)
			{
				LinkConstant method_ref = bootstrap.GetMethodReference(i);
				// method handle
				// FIXME: extend for Eclipse etc. at some point
				if (Javac_Lambda_Class.Equals(method_ref.classname) && (Javac_Lambda_Method.Equals
					(method_ref.elementname) || Javac_Lambda_Alt_Method.Equals(method_ref.elementname
					)))
				{
					lambda_methods.Set(i);
				}
			}
			if (lambda_methods.IsEmpty())
			{
				return;
			}
			// no lambda bootstrap constant found
			IDictionary<string, string> mapMethodsLambda = new Dictionary<string, string>();
			// iterate over code and find invocations of bootstrap methods. Replace them with anonymous classes.
			foreach (StructMethod mt in cl.GetMethods())
			{
				mt.ExpandData();
				InstructionSequence seq = mt.GetInstructionSequence();
				if (seq != null && seq.Length() > 0)
				{
					int len = seq.Length();
					for (int i = 0; i < len; ++i)
					{
						Instruction instr = seq.GetInstr(i);
						if (instr.opcode == ICodeConstants.opc_invokedynamic)
						{
							LinkConstant invoke_dynamic = cl.GetPool().GetLinkConstant(instr.Operand(0));
							if (lambda_methods.Get(invoke_dynamic.index1))
							{
								// lambda invocation found
								List<PooledConstant> bootstrap_arguments = bootstrap.GetMethodArguments(invoke_dynamic
									.index1);
								MethodDescriptor md = MethodDescriptor.ParseDescriptor(invoke_dynamic.descriptor);
								string lambda_class_name = md.ret.value;
								string lambda_method_name = invoke_dynamic.elementname;
								string lambda_method_descriptor = ((PrimitiveConstant)bootstrap_arguments[2]).GetString
									();
								// method type
								LinkConstant content_method_handle = (LinkConstant)bootstrap_arguments[1];
								ClassesProcessor.ClassNode node_lambda = new ClassesProcessor.ClassNode(content_method_handle
									.classname, content_method_handle.elementname, content_method_handle.descriptor, 
									content_method_handle.index1, lambda_class_name, lambda_method_name, lambda_method_descriptor
									, cl);
								node_lambda.simpleName = cl.qualifiedName + "##Lambda_" + invoke_dynamic.index1 +
									 "_" + invoke_dynamic.index2;
								node_lambda.enclosingMethod = InterpreterUtil.MakeUniqueKey(mt.GetName(), mt.GetDescriptor
									());
								node.nested.Add(node_lambda);
								node_lambda.parent = node;
								Sharpen.Collections.Put(clProcessor.GetMapRootClasses(), node_lambda.simpleName, 
									node_lambda);
								if (!node_lambda.lambdaInformation.is_method_reference)
								{
									Sharpen.Collections.Put(mapMethodsLambda, node_lambda.lambdaInformation.content_method_key
										, node_lambda.simpleName);
								}
							}
						}
					}
				}
				mt.ReleaseResources();
			}
			// build class hierarchy on lambda
			foreach (ClassesProcessor.ClassNode nd in node.nested)
			{
				if (nd.type == ClassesProcessor.ClassNode.Class_Lambda)
				{
					string parent_class_name = mapMethodsLambda.GetOrNull(nd.enclosingMethod);
					if (parent_class_name != null)
					{
						ClassesProcessor.ClassNode parent_class = clProcessor.GetMapRootClasses().GetOrNull
							(parent_class_name);
						parent_class.nested.Add(nd);
						nd.parent = parent_class;
					}
				}
			}
		}
		// FIXME: mixed hierarchy?
	}
}
