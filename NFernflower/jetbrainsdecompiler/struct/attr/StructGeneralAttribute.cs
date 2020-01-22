// Copyright 2000-2018 JetBrains s.r.o. Use of this source code is governed by the Apache 2.0 license that can be found in the LICENSE file.
using JetBrainsDecompiler.Struct.Consts;
using JetBrainsDecompiler.Util;
using Sharpen;

namespace JetBrainsDecompiler.Struct.Attr
{
	public class StructGeneralAttribute
	{
		public static readonly StructGeneralAttribute.Key<StructGeneralAttribute> Attribute_Code
			 = new StructGeneralAttribute.Key<StructGeneralAttribute>("Code");

		public static readonly StructGeneralAttribute.Key<StructInnerClassesAttribute> Attribute_Inner_Classes
			 = new StructGeneralAttribute.Key<StructInnerClassesAttribute>("InnerClasses");

		public static readonly StructGeneralAttribute.Key<StructGenericSignatureAttribute
			> Attribute_Signature = new StructGeneralAttribute.Key<StructGenericSignatureAttribute
			>("Signature");

		public static readonly StructGeneralAttribute.Key<StructAnnDefaultAttribute> Attribute_Annotation_Default
			 = new StructGeneralAttribute.Key<StructAnnDefaultAttribute>("AnnotationDefault"
			);

		public static readonly StructGeneralAttribute.Key<StructExceptionsAttribute> Attribute_Exceptions
			 = new StructGeneralAttribute.Key<StructExceptionsAttribute>("Exceptions");

		public static readonly StructGeneralAttribute.Key<StructEnclosingMethodAttribute>
			 Attribute_Enclosing_Method = new StructGeneralAttribute.Key<StructEnclosingMethodAttribute
			>("EnclosingMethod");

		public static readonly StructGeneralAttribute.Key<StructAnnotationAttribute> Attribute_Runtime_Visible_Annotations
			 = new StructGeneralAttribute.Key<StructAnnotationAttribute>("RuntimeVisibleAnnotations"
			);

		public static readonly StructGeneralAttribute.Key<StructAnnotationAttribute> Attribute_Runtime_Invisible_Annotations
			 = new StructGeneralAttribute.Key<StructAnnotationAttribute>("RuntimeInvisibleAnnotations"
			);

		public static readonly StructGeneralAttribute.Key<StructAnnotationParameterAttribute
			> Attribute_Runtime_Visible_Parameter_Annotations = new StructGeneralAttribute.Key
			<StructAnnotationParameterAttribute>("RuntimeVisibleParameterAnnotations");

		public static readonly StructGeneralAttribute.Key<StructAnnotationParameterAttribute
			> Attribute_Runtime_Invisible_Parameter_Annotations = new StructGeneralAttribute.Key
			<StructAnnotationParameterAttribute>("RuntimeInvisibleParameterAnnotations");

		public static readonly StructGeneralAttribute.Key<StructTypeAnnotationAttribute> 
			Attribute_Runtime_Visible_Type_Annotations = new StructGeneralAttribute.Key<StructTypeAnnotationAttribute
			>("RuntimeVisibleTypeAnnotations");

		public static readonly StructGeneralAttribute.Key<StructTypeAnnotationAttribute> 
			Attribute_Runtime_Invisible_Type_Annotations = new StructGeneralAttribute.Key<StructTypeAnnotationAttribute
			>("RuntimeInvisibleTypeAnnotations");

		public static readonly StructGeneralAttribute.Key<StructLocalVariableTableAttribute
			> Attribute_Local_Variable_Table = new StructGeneralAttribute.Key<StructLocalVariableTableAttribute
			>("LocalVariableTable");

		public static readonly StructGeneralAttribute.Key<StructLocalVariableTypeTableAttribute
			> Attribute_Local_Variable_Type_Table = new StructGeneralAttribute.Key<StructLocalVariableTypeTableAttribute
			>("LocalVariableTypeTable");

		public static readonly StructGeneralAttribute.Key<StructConstantValueAttribute> Attribute_Constant_Value
			 = new StructGeneralAttribute.Key<StructConstantValueAttribute>("ConstantValue");

		public static readonly StructGeneralAttribute.Key<StructBootstrapMethodsAttribute
			> Attribute_Bootstrap_Methods = new StructGeneralAttribute.Key<StructBootstrapMethodsAttribute
			>("BootstrapMethods");

		public static readonly StructGeneralAttribute.Key<StructGeneralAttribute> Attribute_Synthetic
			 = new StructGeneralAttribute.Key<StructGeneralAttribute>("Synthetic");

		public static readonly StructGeneralAttribute.Key<StructGeneralAttribute> Attribute_Deprecated
			 = new StructGeneralAttribute.Key<StructGeneralAttribute>("Deprecated");

		public static readonly StructGeneralAttribute.Key<StructLineNumberTableAttribute>
			 Attribute_Line_Number_Table = new StructGeneralAttribute.Key<StructLineNumberTableAttribute
			>("LineNumberTable");

		public static readonly StructGeneralAttribute.Key<StructMethodParametersAttribute
			> Attribute_Method_Parameters = new StructGeneralAttribute.Key<StructMethodParametersAttribute
			>("MethodParameters");

		public class Key<T>
			where T : StructGeneralAttribute
		{
			private readonly string name;

			public Key(string name)
			{
				/*
				attribute_info {
				u2 attribute_name_index;
				u4 attribute_length;
				u1 info[attribute_length];
				}
				*/
				this.name = name;
			}

			public virtual string GetName()
			{
				return name;
			}
		}

		private string name;

		public static StructGeneralAttribute CreateAttribute(string name)
		{
			StructGeneralAttribute attr;
			if (Attribute_Inner_Classes.GetName().Equals(name))
			{
				attr = new StructInnerClassesAttribute();
			}
			else if (Attribute_Constant_Value.GetName().Equals(name))
			{
				attr = new StructConstantValueAttribute();
			}
			else if (Attribute_Signature.GetName().Equals(name))
			{
				attr = new StructGenericSignatureAttribute();
			}
			else if (Attribute_Annotation_Default.GetName().Equals(name))
			{
				attr = new StructAnnDefaultAttribute();
			}
			else if (Attribute_Exceptions.GetName().Equals(name))
			{
				attr = new StructExceptionsAttribute();
			}
			else if (Attribute_Enclosing_Method.GetName().Equals(name))
			{
				attr = new StructEnclosingMethodAttribute();
			}
			else if (Attribute_Runtime_Visible_Annotations.GetName().Equals(name) || Attribute_Runtime_Invisible_Annotations
				.GetName().Equals(name))
			{
				attr = new StructAnnotationAttribute();
			}
			else if (Attribute_Runtime_Visible_Parameter_Annotations.GetName().Equals(name) ||
				 Attribute_Runtime_Invisible_Parameter_Annotations.GetName().Equals(name))
			{
				attr = new StructAnnotationParameterAttribute();
			}
			else if (Attribute_Runtime_Visible_Type_Annotations.GetName().Equals(name) || Attribute_Runtime_Invisible_Type_Annotations
				.GetName().Equals(name))
			{
				attr = new StructTypeAnnotationAttribute();
			}
			else if (Attribute_Local_Variable_Table.GetName().Equals(name))
			{
				attr = new StructLocalVariableTableAttribute();
			}
			else if (Attribute_Local_Variable_Type_Table.GetName().Equals(name))
			{
				attr = new StructLocalVariableTypeTableAttribute();
			}
			else if (Attribute_Bootstrap_Methods.GetName().Equals(name))
			{
				attr = new StructBootstrapMethodsAttribute();
			}
			else if (Attribute_Synthetic.GetName().Equals(name) || Attribute_Deprecated.GetName
				().Equals(name))
			{
				attr = new StructGeneralAttribute();
			}
			else if (Attribute_Line_Number_Table.GetName().Equals(name))
			{
				attr = new StructLineNumberTableAttribute();
			}
			else if (Attribute_Method_Parameters.GetName().Equals(name))
			{
				attr = new StructMethodParametersAttribute();
			}
			else
			{
				// unsupported attribute
				return null;
			}
			attr.name = name;
			return attr;
		}

		/// <exception cref="System.IO.IOException"/>
		public virtual void InitContent(DataInputFullStream data, ConstantPool pool)
		{
		}

		public virtual string GetName()
		{
			return name;
		}
	}
}
