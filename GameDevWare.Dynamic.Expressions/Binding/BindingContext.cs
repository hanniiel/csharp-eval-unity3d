﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq.Expressions;

namespace GameDevWare.Dynamic.Expressions.Binding
{
	internal sealed class BindingContext
	{
		private readonly Expression global;
		private readonly ReadOnlyCollection<ParameterExpression> parameters;
		private readonly Type resultType;
		private readonly ITypeResolver typeResolver;

		public ReadOnlyCollection<ParameterExpression> Parameters { get { return this.parameters; } }
		public Type ResultType { get { return this.resultType; } }
		public Expression Global { get { return this.global; } }

		public BindingContext(ITypeResolver typeResolver, ReadOnlyCollection<ParameterExpression> parameters, Type resultType, Expression global)
		{
			if (typeResolver == null) throw new ArgumentNullException("typeResolver");
			if (parameters == null) throw new ArgumentNullException("parameters");
			if (resultType == null) throw new ArgumentNullException("resultType");
			if (global == null) throw new ArgumentNullException("global");

			this.typeResolver = typeResolver;
			this.parameters = parameters;
			this.resultType = resultType;
			this.global = global;
		}

		public bool TryResolveType(object typeName, out Type type)
		{
			if (typeName == null) throw new ArgumentNullException("typeName");

			type = default(Type);
			var typeReference = default(TypeReference);
			if (TryGetTypeReference(typeName, out typeReference) == false || this.typeResolver.TryGetType(typeReference, out type) == false)
				return false;

			return type != null;
		}

		public static bool TryGetTypeReference(object value, out TypeReference typeReference)
		{
			if (value == null) throw new ArgumentNullException("value");

			typeReference = default(TypeReference);

			if (value is SyntaxTreeNode)
			{
				var parts = new List<SyntaxTreeNode>(10);
				var current = (SyntaxTreeNode)value;
				while (current != null)
				{
					var expressionType = current.GetExpressionType(throwOnError: true);
					if (expressionType != Constants.EXPRESSION_TYPE_PROPERTY_OR_FIELD)
						return false;

					parts.Add(current);
					current = current.GetExpression(throwOnError: false);
				}

				var typeNameParts = default(List<string>);
				var typeArguments = default(List<TypeReference>);
				for (var p = 0; p < parts.Count; p++)
				{
					var part = parts[parts.Count - 1 - p]; // reverse order
					var arguments = part.GetArguments(throwOnError: false);
					var typeNamePart = part.GetPropertyOrFieldName(throwOnError: true);
					if (typeNameParts == null) typeNameParts = new List<string>();
					var typeArgumentsCount = 0;
					if (arguments != null && arguments.Count > 0)
					{
						if (typeArguments == null) typeArguments = new List<TypeReference>(10);

						for (var i = 0; i < arguments.Count; i++)
						{
							var typeArgument = default(SyntaxTreeNode);
							var typeArgumentTypeReference = default(TypeReference);
							var key = Constants.GetIndexAsString(i);
							if (arguments.TryGetValue(key, out typeArgument) == false || typeArgument == null)
								throw new ExpressionParserException(string.Format(Properties.Resources.EXCEPTION_BIND_MISSINGORWRONGARGUMENT, key), part);

							if (typeArgument.GetExpressionType(throwOnError: true) == Constants.EXPRESSION_TYPE_PROPERTY_OR_FIELD && typeArgument.GetPropertyOrFieldName(throwOnError: true) == string.Empty)
								typeArgumentTypeReference = TypeReference.Empty;
							else if (TryGetTypeReference(typeArgument, out typeArgumentTypeReference) == false)
								throw new ExpressionParserException(string.Format(Properties.Resources.EXCEPTION_BIND_MISSINGORWRONGARGUMENT, key), part);

							typeArguments.Add(typeArgumentTypeReference);
							typeArgumentsCount++;
						}
						typeNamePart = typeNamePart + "`" + Constants.GetIndexAsString(typeArgumentsCount);
					}
					typeNameParts.Add(typeNamePart);
				}

				typeReference = new TypeReference(typeNameParts, typeArguments ?? TypeReference.EmptyTypeArguments);
				return true;
			}
			else
			{
				typeReference = new TypeReference(new[] { Convert.ToString(value, Constants.DefaultFormatProvider) }, TypeReference.EmptyTypeArguments);
				return true;
			}
		}
		public static bool TryGetMethodReference(object value, out TypeReference methodReference)
		{
			if (value == null) throw new ArgumentNullException("value");

			methodReference = default(TypeReference);

			if (value is SyntaxTreeNode)
			{
				var typeArguments = default(List<TypeReference>);
				var methodNameTree = (SyntaxTreeNode)value;

				var arguments = methodNameTree.GetArguments(throwOnError: false);
				var methodName = methodNameTree.GetPropertyOrFieldName(throwOnError: true);
				if (arguments != null && arguments.Count > 0)
				{
					typeArguments = new List<TypeReference>(10);

					for (var i = 0; i < arguments.Count; i++)
					{
						var typeArgument = default(SyntaxTreeNode);
						var typeArgumentTypeReference = default(TypeReference);
						var key = Constants.GetIndexAsString(i);
						if (arguments.TryGetValue(key, out typeArgument) == false || typeArgument == null)
							throw new ExpressionParserException(string.Format(Properties.Resources.EXCEPTION_BIND_MISSINGORWRONGARGUMENT, key), methodNameTree);

						var isEmptyTypeArgument = typeArgument.GetExpressionType(throwOnError: true) == Constants.EXPRESSION_TYPE_PROPERTY_OR_FIELD && typeArgument.GetPropertyOrFieldName(throwOnError: true) == string.Empty;
						if (isEmptyTypeArgument || TryGetTypeReference(typeArgument, out typeArgumentTypeReference) == false)
							throw new ExpressionParserException(string.Format(Properties.Resources.EXCEPTION_BIND_MISSINGORWRONGARGUMENT, key), methodNameTree);

						typeArguments.Add(typeArgumentTypeReference);
					}
				}

				methodReference = new TypeReference(new[] { methodName }, typeArguments ?? TypeReference.EmptyTypeArguments);
				return true;
			}
			else
			{
				methodReference = new TypeReference(new[] { Convert.ToString(value, Constants.DefaultFormatProvider) }, TypeReference.EmptyTypeArguments);
				return true;
			}
		}

		public BindingContext CreateNestedContext(List<ParameterExpression> builderParameters, Type resultType)
		{
			if (builderParameters == null) throw new ArgumentNullException("builderParameters");
			if (resultType == null) throw new ArgumentNullException("resultType");

			return new BindingContext(this.typeResolver, parameters, resultType, global);
		}
	}
}