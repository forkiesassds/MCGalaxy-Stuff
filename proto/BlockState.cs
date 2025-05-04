using System;
using System.Collections.Generic;
using System.Linq;
using MCGalaxy;

namespace VeryPlugins
{
    public class BlockState
    {
        readonly List<StateProperty> _properties = new List<StateProperty>();
        
        public string ID { get; private set; }
        public List<StateProperty> Properties { get { return _properties; } }

        public override bool Equals(object obj)
        {
            BlockState state = obj as BlockState;
            if (state == null) return false;
            
            return ID == state.ID && _properties.Count == state._properties.Count 
                && _properties.All(state._properties.Contains);
        }
        
        public override int GetHashCode()
        {
            return HashCode.Combine(_properties, ID);
        }
        
        public static BlockState Parse(string str)
        {
            return Parse(null, str);
        }

        public static BlockState Parse(BlockState known, string str)
        {
            if (str == null) return null;
            BlockState state = new BlockState();

            int propIdx = str.IndexOf('[');
            if (propIdx == -1)
            {
                state.ID = str;
                return state;
            }
            
            string propsStr = str.Substring(propIdx + 1, str.Length - propIdx - 2);
            str = str.Substring(0, propIdx);
            
            state.ID = str;

            string[] props = propsStr.SplitComma();
            foreach (string prop in props)
            {
                string[] kvp = prop.Split('=', 2);
                StateProperty sProp = new StateProperty
                {
                    key = kvp[0],
                    value = kvp[1]
                };

                if (known != null && !known._properties.Contains(sProp))
                    continue;
                
                state.Properties.Add(sProp);
            }
            
            return state;
        }
    }

    public struct StateProperty : IEquatable<StateProperty>
    {
        public string key;
        public string value;

        public override bool Equals(object obj)
        {
            if (obj == null) return false;
            
            StateProperty prop = (StateProperty)obj;
            return key == prop.key && value == prop.value;
        }
        
        public override int GetHashCode()
        {
            return HashCode.Combine(key, value);
        }
        
        public bool Equals(StateProperty other)
        {
            return key == other.key && value == other.value;
        }

        public static bool operator ==(StateProperty left, StateProperty right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(StateProperty left, StateProperty right)
        {
            return !(left == right);
        }
    }
}