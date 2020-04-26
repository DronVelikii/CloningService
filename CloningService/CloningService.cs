using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace CloningService
{
    public class CloningService : ICloningService
    {
        #region members

        private int lastSize;

        // type wrappers are used to improve performance by caching type attributes (properties/fields) and compiled lambda expressions
        private static readonly Dictionary<Type, TypeWrapper> typeWrappers = new Dictionary<Type, TypeWrapper>();

        /// <summary>
        /// Get type wrapper
        /// </summary> 
        private static TypeWrapper GetTypeWrapper(Type type)
        {
            bool found = typeWrappers.TryGetValue(type, out var typeWrapper);

            if (!found)
            {
                typeWrapper = new TypeWrapper(type);

                typeWrappers.Add(type, typeWrapper);

                foreach (var fieldWrapper in typeWrapper.FieldWrappers)
                {
                    fieldWrapper.Init();
                }

                foreach (var propertyWrapper in typeWrapper.PropertyWrappers)
                {
                    propertyWrapper.Init();
                }
            }

            return typeWrapper;
        }

        #endregion

        #region classes

        /// <summary>
        /// Wrapper for fields ready for cloning
        /// </summary>
        private class FieldWrapper
        {
            public Func<object, object> GetValue; // fast field setter
            public Action<object, object> SetValue; // fast field getter
            public readonly CloningMode CloningMode;
            public TypeWrapper TypeWrapper; // wrapper for type of this field
            private readonly FieldInfo FieldInfo;


            public FieldWrapper(Type type, FieldInfo fieldInfo, CloningMode cloningMode)
            {
                FieldInfo = fieldInfo;
                CloningMode = cloningMode;

                createGetValueExpression(type, fieldInfo);
                createSetValueExpression(type, fieldInfo);
            }

            private void createGetValueExpression(Type type, FieldInfo fieldInfo)
            {
                var sourceParamExpr = Expression.Parameter(typeof(object));

                // (SomeType)source
                var sourceCastExpr = Expression.Convert(sourceParamExpr, type);

                // (SomeType)source.Field
                var fieldExpression = Expression.Field(sourceCastExpr, fieldInfo);

                // (object)(SomeType)source.Field)
                var fieldCastExpr = Expression.Convert(fieldExpression, typeof(object));

                // using example: fieldWrapper.GetValue(source)
                GetValue = Expression.Lambda<Func<object, object>>(fieldCastExpr, sourceParamExpr).Compile();
            }

            private void createSetValueExpression(Type type, FieldInfo fieldInfo)
            {
                /// var target = new SomeType();
                /// (SomeType)target.Field = (FieldType)value;

                var valueParameterExpr = Expression.Parameter(typeof(object));
                var targetParameterExpr = Expression.Parameter(typeof(object));

                // (SomeType)target
                var targetCastExpr = type.IsValueType ? Expression.Unbox(targetParameterExpr, type) : Expression.Convert(targetParameterExpr, type);

                // (SomeType)target.Field 
                var fieldExpression = Expression.Field(targetCastExpr, fieldInfo);

                // (FieldType)value
                Expression valueCastExpr = Expression.Convert(valueParameterExpr, fieldInfo.FieldType);

                // (SomeType)target.Field = (FieldType)value;
                var assignExpr = Expression.Assign(fieldExpression, valueCastExpr);

                // using example: fieldWrapper.SetValue(target, value);
                SetValue = Expression.Lambda<Action<object, object>>(assignExpr, targetParameterExpr, valueParameterExpr).Compile();
            }

            public void Init()
            {
                TypeWrapper = GetTypeWrapper(FieldInfo.FieldType);
            }
        }

        /// <summary>
        /// Wrapper for properties ready for cloning
        /// </summary>
        private class PropertyWrapper
        {
            public Func<object, object> GetValue; // fast property getter
            public Action<object, object> SetValue; // fast property setter
            public readonly CloningMode CloningMode;
            public TypeWrapper TypeWrapper; // wrapper for type of this property
            private readonly PropertyInfo PropertyInfo;

            public PropertyWrapper(Type type, PropertyInfo propertyInfo, CloningMode cloningMode)
            {
                PropertyInfo = propertyInfo;
                CloningMode = cloningMode;

                createGetValueExpression(type, propertyInfo);
                createSetValueExpression(type, propertyInfo);
            }

            private void createGetValueExpression(Type type, PropertyInfo propertyInfo)
            {
                var sourceParamExpr = Expression.Parameter(typeof(object));

                // (SomeType)source
                var sourceCastExpr = Expression.Convert(sourceParamExpr, type);

                // (SomeType)source.Property
                var propertyExpression = Expression.Property(sourceCastExpr, propertyInfo);

                // (object)(SomeType)source.Property)
                var propertyCastExpr = Expression.Convert(propertyExpression, typeof(object));

                // using example: propertyWrapper.GetValue(source)
                GetValue = Expression.Lambda<Func<object, object>>(propertyCastExpr, sourceParamExpr).Compile();
            }

            private void createSetValueExpression(Type type, PropertyInfo propertyInfo)
            {
                /// var target = new SomeType();
                /// (SomeType)target.Property = (PropertyType)value;

                var valueParameterExpr = Expression.Parameter(typeof(object));
                var targetParameterExpr = Expression.Parameter(typeof(object));

                // (SomeType)target
                var targetCastExpr = type.IsValueType ? Expression.Unbox(targetParameterExpr, type) : Expression.Convert(targetParameterExpr, type);

                // (SomeType)target.Property 
                var propertyExpression = Expression.Property(targetCastExpr, propertyInfo);

                // (PropertyType)value
                Expression valueCastExpr = Expression.Convert(valueParameterExpr, propertyInfo.PropertyType);

                // (SomeType)target.Property = (PropertyType)value;
                var assignExpr = Expression.Assign(propertyExpression, valueCastExpr);

                // using example: propertyWrapper.SetValue(target, value);
                SetValue = Expression.Lambda<Action<object, object>>(assignExpr, targetParameterExpr, valueParameterExpr).Compile();
            }

            public void Init()
            {
                TypeWrapper = GetTypeWrapper(PropertyInfo.PropertyType);
            }
        }

        /// <summary>
        /// Wrapper contains attributes of the type, including fields and properties
        /// </summary>
        private class TypeWrapper
        {
            public readonly Type Type;
            public readonly Func<object> CreateInstance;
            public readonly Func<int, object> CreateArrayInstance;
            public readonly List<FieldWrapper> FieldWrappers = new List<FieldWrapper>();
            public readonly List<PropertyWrapper> PropertyWrappers = new List<PropertyWrapper>();
            public readonly int FieldWrappersCount;
            public readonly int PropertyWrappersCount;

            public readonly bool ShallowCloning;
            public readonly bool IsCollection;
            public readonly bool IsArray;

            public TypeWrapper(Type type)
            {
                Type = type;

                ShallowCloning = type.IsPrimitive || type == typeof(string);

                if (ShallowCloning)
                {
                    return;
                }

                IsArray = type.IsArray;
                IsCollection = type.GetInterfaces().Any(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(ICollection<>));

                if (IsArray)
                {
                    var constructor = type.GetConstructor(new[] {typeof(int)});

                    if (constructor == null)
                    {
                        throw new InvalidOperationException("array constructor not found");
                    }

                    var sizeParamExpr = Expression.Parameter(typeof(int), "size");
                    var arrayCtorExpr = Expression.New(constructor, sizeParamExpr);
                    CreateArrayInstance = Expression.Lambda<Func<int, object>>(arrayCtorExpr, sizeParamExpr).Compile();
                }
                else if (!type.IsPrimitive)
                {
                    if (type.IsValueType)
                    {
                        CreateInstance = Expression.Lambda<Func<object>>(Expression.Convert(Expression.New(type), typeof(object))).Compile();
                    }
                    else
                    {
                        CreateInstance = Expression.Lambda<Func<object>>(Expression.New(type)).Compile();
                    }
                }

                #region fields

                if (IsCollection || IsArray)
                {
                    return;
                }

                var fields = type.GetFields().Where(x => x.IsPublic);
                foreach (var field in fields)
                {
                    var cloningMode = GetMode(field);

                    if (cloningMode == CloningMode.Ignore)
                    {
                        continue;
                    }

                    FieldWrappers.Add(new FieldWrapper(type, field, cloningMode));
                }

                FieldWrappersCount = FieldWrappers.Count;

                #endregion

                #region properties

                var properties = type.GetProperties().Where(x => x.CanWrite && x.CanRead);
                foreach (var property in properties)
                {
                    var cloningMode = GetMode(property);

                    if (cloningMode == CloningMode.Ignore)
                    {
                        continue;
                    }

                    PropertyWrappers.Add(new PropertyWrapper(type, property, cloningMode));
                }

                PropertyWrappersCount = PropertyWrappers.Count;

                #endregion
            }

            private static CloningMode GetMode(MemberInfo member)
            {
                var cloneableAttribute = member.GetCustomAttributes().FirstOrDefault(x => x is CloneableAttribute);
                var mode = (cloneableAttribute as CloneableAttribute)?.Mode;
                return mode ?? CloningMode.Deep;
            }
        }

        #endregion

        #region methods

        /// <summary>
        /// copy field value from source to target. Deep cloning used if necessary
        /// </summary>
        private static void CopyFieldValue(object source, object target, FieldWrapper fieldWrapper, Dictionary<object, object> clonedObjects)
        {
            var value = fieldWrapper.GetValue(source);

            if (value == null)
            {
                return;
            }

            if (fieldWrapper.CloningMode == CloningMode.Deep)
            {
                value = Clone(fieldWrapper.TypeWrapper, value, clonedObjects);
            }

            fieldWrapper.SetValue(target, value);
        }

        /// <summary>
        /// copy property value from source to target. Deep cloning used if necessary
        /// </summary>
        private static void CopyPropertyValue(object source, object target, PropertyWrapper propertyWrapper, Dictionary<object, object> clonedObjects)
        {
            var value = propertyWrapper.GetValue(source);

            if (value == null)
            {
                return;
            }

            if (propertyWrapper.CloningMode == CloningMode.Deep)
            {
                value = Clone(propertyWrapper.TypeWrapper, value, clonedObjects);
            }

            propertyWrapper.SetValue(target, value);
        }

        /// <summary>
        /// clone object
        /// </summary>
        /// <param name="typeWrapper">type information of source object</param> 
        private static object Clone(TypeWrapper typeWrapper, object source, Dictionary<object, object> clonedObjects)
        {
            if (typeWrapper.ShallowCloning)
            {
                return source;
            }

            var found = clonedObjects.TryGetValue(source, out var newInstance); // check if already cloned
            if (found)
            {
                return newInstance;
            }

            if (typeWrapper.IsCollection)
            {
                if (typeWrapper.IsArray)
                {
                    newInstance = typeWrapper.CreateArrayInstance((source as Array).Length);
                    clonedObjects.Add(source, newInstance);
                    var sourceArray = (source as Array);
                    var targetArray = (newInstance as Array);

                    for (int i = 0; i < sourceArray.Length; i++)
                    {
                        var item = sourceArray.GetValue(i);
                        var clonedItem = Clone(GetTypeWrapper(item.GetType()), item, clonedObjects);
                        targetArray?.SetValue(clonedItem, i);
                    }

                    return newInstance;
                }

                newInstance = typeWrapper.CreateInstance();
                clonedObjects.Add(source, newInstance);

                var methodInfo = typeWrapper.Type.GetMethod("Add");
                var sourceCollection = (source as ICollection);
                foreach (var item in sourceCollection)
                {
                    var clonedItem = Clone(GetTypeWrapper(item.GetType()), item, clonedObjects);
                    methodInfo?.Invoke(newInstance, new[] {clonedItem});
                }

                return newInstance;
            }

            newInstance = typeWrapper.CreateInstance();
            clonedObjects.Add(source, newInstance);

            int index;

            for (index = 0; index < typeWrapper.FieldWrappersCount; index++)
            {
                CopyFieldValue(source, newInstance, typeWrapper.FieldWrappers[index], clonedObjects);
            }

            for (index = 0; index < typeWrapper.PropertyWrappersCount; index++)
            {
                CopyPropertyValue(source, newInstance, typeWrapper.PropertyWrappers[index], clonedObjects);
            }

            return newInstance;
        }

        #endregion

        /// <summary>
        /// <see cref="ICloningService.Clone"/>
        /// </summary>
        public T Clone<T>(T source)
        {
            var clonedObjects = new Dictionary<object, object>(lastSize * 2);
            var result = Clone(GetTypeWrapper(typeof(T)), source, clonedObjects);
            lastSize = clonedObjects.Count; // HACK: in order to predict the next dictionary capacity
            return (T) result;
        }
    }
}