﻿using GeneralServices.Attributes;
using GeneralServices.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace GeneralServices.Helpers
{
    public static class Reflection
    {
        /// <summary>
        /// Returns the name of the current running method
        /// </summary>
        /// <returns></returns>
        public static string GetCurrentMethodName()
        {
            return GetMethodNameFromStack(2);
        }

        /// <summary>
        /// Returns the name of the calling method
        /// </summary>
        /// <returns></returns>
        public static string GetCallingMethodName()
        {
            return GetMethodNameFromStack(3);
        }

        /// <summary>
        /// Frames: 0=this, 1=GetCurrentMethodName, 2=real calling method name
        /// </summary>
        /// <param name="frame"></param>
        /// <returns></returns>
        private static string GetMethodNameFromStack(int frame)
        {
            if (frame < 0)
            {
                throw new System.Exception("Invalid stack frame number");
            }
            else
            {
                try
                {
                    StackTrace st = new StackTrace();
                    StackFrame sf = st.GetFrame(frame);
                    return sf.GetMethod().Name;
                }
                catch (System.Exception _ex)
                {
                    throw new System.Exception(string.Format("Unable to get method name from running stack : {0}", _ex.Message));
                }
            }
        }

        /// <summary>
        /// Updates a given property in an object
        /// </summary>
        /// <param name="model">The target object to be updated</param>
        /// <param name="PropertyName">The property (field) name that needs to be updated</param>
        /// <param name="PropertyValue">The value to be used to update the target object's property</param>
        /// <returns></returns>
        public static bool UpdateObjectProperty(dynamic model, string PropertyName, dynamic PropertyValue)
        {
            bool updateResult = false;

            try
            {
                PropertyInfo pInfo = model.GetType().GetProperty(PropertyName, BindingFlags.Public | BindingFlags.Instance);
                if (pInfo != null)
                {
                    if (IsNumericType(pInfo.PropertyType))
                    {
                        // assuming double
                        double PropVal;
                        double.TryParse(PropertyValue, out PropVal);
                        pInfo.SetValue(model, PropVal);
                    }
                    else
                    {
                        pInfo.SetValue(model, PropertyValue);
                    }
                    updateResult = true;
                }
            }
            catch (Exception Ex)
            {
                throw new Exception(String.Format("Unable to update property {0} of object : {1}", PropertyName, Ex.Message));
            }

            return updateResult;
        }

        /// <summary>
        /// Determines if a type is numeric.  Nullable numeric types are considered numeric.
        /// </summary>
        /// <remarks>
        /// Boolean is not considered numeric.
        /// </remarks>
        public static bool IsNumericType(Type type)
        {
            if (type == null)
            {
                return false;
            }

            switch (Type.GetTypeCode(type))
            {
                case TypeCode.Byte:
                case TypeCode.Decimal:
                case TypeCode.Double:
                case TypeCode.Int16:
                case TypeCode.Int32:
                case TypeCode.Int64:
                case TypeCode.SByte:
                case TypeCode.Single:
                case TypeCode.UInt16:
                case TypeCode.UInt32:
                case TypeCode.UInt64:
                    return true;
                case TypeCode.Object:
                    if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
                    {
                        return IsNumericType(Nullable.GetUnderlyingType(type));
                    }
                    return false;
            }
            return false;
        }

        /// <summary>
        /// Gets an array of all class types from a given assembly
        /// </summary>
        /// <param name="domainModelAssemblyName"></param>
        /// <returns></returns>
        public static Type[] GetDomainTypes(string domainModelAssemblyName)
        {
            Assembly DomainModelAssembly = null;
            Type[] DomainTypes = null;

            if (string.IsNullOrEmpty(domainModelAssemblyName))
            {
                throw new Exception(string.Format("{0} : Domain model assembly name cannot be empty.", Reflection.GetCurrentMethodName()));
            }

            try
            {
                DomainModelAssembly = AppDomain.CurrentDomain.GetAssemblies().SingleOrDefault(assembly => assembly.GetName().Name == domainModelAssemblyName);
            }
            catch (Exception Ex)
            {
                throw new Exception(string.Format("{0} : Unable to load assembly {1} : {2}", Reflection.GetCurrentMethodName(), domainModelAssemblyName, Ex.Message), Ex);
            }

            if (DomainModelAssembly == null)
            {
                throw new Exception(string.Format("{0} : Unable to load assembly {1}", Reflection.GetCurrentMethodName(), domainModelAssemblyName));
            }

            try
            {
                DomainTypes = DomainModelAssembly.GetTypes();
            }
            catch (Exception Ex)
            {
                throw new Exception(string.Format("{0} : Unable to load domain types from assembly {1} : {2}", Reflection.GetCurrentMethodName(), domainModelAssemblyName, Ex.Message), Ex);
            }

            return DomainTypes;
        }

        public static List<KeyValuePair<int, object>> GetObjectPropertiesAndValues(dynamic Object)
        {
            if (Object == null)
            {
                throw new Exception("{0} : Passed object must not be null.");
            }

            Type type = Object.GetType();
            List<KeyValuePair<int, object>> properties = new List<KeyValuePair<int, object>>();

            IList<PropertyInfo> props = new List<PropertyInfo>(type.GetProperties());

            foreach (PropertyInfo prop in props)
            {
                object propValue = prop.GetValue(Object, null);
                properties.Add(new KeyValuePair<int, object>(General.calculateSingleFieldHash(prop).Value, propValue));
            }

            return properties;
        }

        public static List<EntityPropertyChange> GetEntityPropertyValuesAsChanges(object Entity)
        {
            List<EntityPropertyChange> entityPropertyChanges = new List<EntityPropertyChange>();

            if (Entity != null)
            {
                var Values = Entity.GetType().GetProperties();
                var dateChanged = DateTime.Now;

                foreach (PropertyInfo p in Values)
                {
                    try
                    {
                        if (Attribute.IsDefined(p, typeof(IgnoreChanges)) == false)
                        {
                            var newValue = p.GetValue(Entity) != null ? p.GetValue(Entity).ToString() : null;

                            entityPropertyChanges.Add(new EntityPropertyChange
                            {
                                CurrentValueAsText = newValue,
                                EntityPropertyID = General.calculateSingleFieldHash(p).Value,
                                Date = dateChanged
                            });
                        }
                    }
                    catch (Exception Ex)
                    {
                        var m = Ex;
                    }
                }
            }

            return entityPropertyChanges;
        }

        public static List<EntityPropertyChange> GetEntityPropertyChanges(object OldEntity, object NewEntity)
        {
            if (OldEntity.GetType() != NewEntity.GetType())
            {
                throw new Exception(string.Format("{0} : Cannot get property changes for different entitiy types."));
            }

            List<EntityPropertyChange> entityPropertyChanges = new List<EntityPropertyChange>();

            var oldValues = OldEntity.GetType().GetProperties();
            var newValues = NewEntity.GetType().GetProperties();
            var dateChanged = DateTime.Now;

            foreach (PropertyInfo p in oldValues)
            {
                try
                {
                    var matchingProperty = newValues.Where(npv =>
                        npv.Name.Equals(p.Name) &&
                        npv.PropertyType == p.PropertyType &&
                        Attribute.IsDefined(npv, typeof(IgnoreChanges)) == false).FirstOrDefault();

                    if (matchingProperty != null)
                    {
                        var oldValue = p.GetValue(OldEntity) != null ? p.GetValue(OldEntity).ToString() : null;
                        var newValue = matchingProperty.GetValue(NewEntity) != null ? matchingProperty.GetValue(NewEntity).ToString() : null;

                        if (newValue != oldValue)
                        {
                            entityPropertyChanges.Add(new EntityPropertyChange
                            {
                                OriginalValueAsText = oldValue,
                                CurrentValueAsText = newValue,
                                EntityPropertyID = General.calculateSingleFieldHash(p).Value,
                                Date = dateChanged
                            });
                        }
                    }
                }
                catch (Exception Ex)
                {
                    var m = Ex;
                }
            }

            return entityPropertyChanges;
        }
    }
}