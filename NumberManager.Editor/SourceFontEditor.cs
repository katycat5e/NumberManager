﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace NumberManager.Editor
{
    public class SourceFontAttribute : PropertyAttribute
    {

    }

    [CustomPropertyDrawer(typeof(SourceFontAttribute))]
    public class SourceFontEditor : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property == null) return;

            //var currentValue = fieldInfo.GetValue(property.serializedObject.targetObject) as Font;
            var currentValue = GetTargetObjectOfProperty(property) as Font;

            EditorGUI.BeginProperty(position, label, property);

            position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);

            string buttonText = currentValue ? currentValue.name : "Select Font";
            if (GUI.Button(position, buttonText))
            {
                if (SystemFontLoader.PromptForFont(out Font newFont))
                {
                    SetTargetObjectOfProperty(property, newFont);
                    GUIUtility.ExitGUI();
                    return;
                    //fieldInfo.SetValue(property.serializedObject.targetObject, newFont);
                }
            }

            EditorGUI.EndProperty();
        }

        private static object GetTargetObjectOfProperty(SerializedProperty prop)
        {
            if (prop == null) return null;

            var path = prop.propertyPath.Replace(".Array.data[", "[");
            object obj = prop.serializedObject.targetObject;
            var elements = path.Split('.');
            foreach (var element in elements)
            {
                if (element.Contains("["))
                {
                    var elementName = element.Substring(0, element.IndexOf("["));
                    var index = System.Convert.ToInt32(element.Substring(element.IndexOf("[")).Replace("[", "").Replace("]", ""));
                    obj = GetValue_Imp(obj, elementName, index);
                }
                else
                {
                    obj = GetValue_Imp(obj, element);
                }
            }
            return obj;
        }

        private static object GetValue_Imp(object source, string name)
        {
            if (source == null)
                return null;
            var type = source.GetType();

            while (type != null)
            {
                var f = type.GetField(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                if (f != null)
                    return f.GetValue(source);

                var p = type.GetProperty(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (p != null)
                    return p.GetValue(source, null);

                type = type.BaseType;
            }
            return null;
        }

        private static object GetValue_Imp(object source, string name, int index)
        {
            var enumerable = GetValue_Imp(source, name) as System.Collections.IEnumerable;
            if (enumerable == null) return null;
            var enm = enumerable.GetEnumerator();
            //while (index-- >= 0)
            //    enm.MoveNext();
            //return enm.Current;

            for (int i = 0; i <= index; i++)
            {
                if (!enm.MoveNext()) return null;
            }
            return enm.Current;
        }

        public static void SetTargetObjectOfProperty(SerializedProperty prop, object value)
        {
            var path = prop.propertyPath.Replace(".Array.data[", "[");
            object obj = prop.serializedObject.targetObject;
            var elements = path.Split('.');
            foreach (var element in elements.Take(elements.Length - 1))
            {
                if (element.Contains("["))
                {
                    var elementName = element.Substring(0, element.IndexOf("["));
                    var index = System.Convert.ToInt32(element.Substring(element.IndexOf("[")).Replace("[", "").Replace("]", ""));
                    obj = GetValue_Imp(obj, elementName, index);
                }
                else
                {
                    obj = GetValue_Imp(obj, element);
                }
            }

            if (ReferenceEquals(obj, null)) return;

            try
            {
                var element = elements.Last();

                if (element.Contains("["))
                {
                    var tp = obj.GetType();
                    var elementName = element.Substring(0, element.IndexOf("["));
                    var index = System.Convert.ToInt32(element.Substring(element.IndexOf("[")).Replace("[", "").Replace("]", ""));
                    var field = tp.GetField(elementName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    var arr = field.GetValue(obj) as System.Collections.IList;
                    //arr[index] = value;
                    //var elementName = element.Substring(0, element.IndexOf("["));
                    //var index = System.Convert.ToInt32(element.Substring(element.IndexOf("[")).Replace("[", "").Replace("]", ""));
                    //var arr = DynamicUtil.GetValue(element, elementName) as System.Collections.IList;
                    if (arr != null) arr[index] = value;
                }
                else
                {
                    var tp = obj.GetType();
                    var field = tp.GetField(element, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (field != null)
                    {
                        field.SetValue(obj, value);
                    }
                    //DynamicUtil.SetValue(obj, element, value);
                }

            }
            catch
            {
                return;
            }
        }
    }
}
