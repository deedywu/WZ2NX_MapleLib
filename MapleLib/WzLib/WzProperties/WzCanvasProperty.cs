﻿/*  MapleLib - A general-purpose MapleStory library
 * Copyright (C) 2009, 2010, 2015 Snow and haha01haha01
   
 * This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

 * This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

 * You should have received a copy of the GNU General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.*/

using System;
using System.Collections.Generic;
using System.Linq;
using MapleLib.WzLib.WzProperties;

namespace Mh.MapleLib.WzLib.WzProperties
{
    /// <summary>
    /// A property that can contain sub properties and has one png image
    /// </summary>
    public class WzCanvasProperty : WzExtended, IPropertyContainer
    {
        #region Fields

        private List<WzImageProperty> _properties = new List<WzImageProperty>();

        #endregion

        public override int ChildCount()
        {
            return _properties.Count;
        }

        public override List<WzObject> ChildArray()
        {
            return new List<WzObject>(_properties);
        }

        #region Inherited Members

        public override object WzValue => PngProperty;

        /// <summary>
        /// The parent of the object
        /// </summary>
        public override WzObject Parent { get; internal set; }

        /// <summary>
        /// The WzPropertyType of the property
        /// </summary>
        public override WzPropertyType PropertyType => WzPropertyType.Canvas;

        /// <summary>
        /// The properties contained in this property
        /// </summary>
        public override List<WzImageProperty> WzProperties => _properties;

        /// <summary>
        /// The name of the property
        /// </summary>
        public sealed override string Name { get; set; }

        /// <summary>
        /// Gets a wz property by it's name
        /// </summary>
        /// <param name="name">The name of the property</param>
        /// <returns>The wz property with the specified name</returns>
        public override WzImageProperty this[string name]
        {
            get
            {
                return name == "PNG"
                    ? PngProperty
                    : _properties.FirstOrDefault(iwp => iwp.Name.ToLower().Equals(name.ToLower()));
            }
            set
            {
                if (value == null) return;
                if (name == "PNG")
                {
                    PngProperty = (WzPngProperty)value;
                    return;
                }

                value.Name = name;
                AddProperty(value);
            }
        }

        /// <summary>
        /// Gets a wz property by a path name
        /// </summary>
        /// <param name="path">path to property</param>
        /// <returns>the wz property with the specified name</returns>
        public override WzImageProperty GetFromPath(string path)
        {
            var segments = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (segments[0] == "..")
            {
                return ((WzImageProperty)Parent)[path.Substring(Name.IndexOf('/') + 1)];
            }

            WzImageProperty ret = this;
            foreach (var t in segments)
            {
                var foundChild = false;
                if (t == "PNG") return PngProperty;

                foreach (var iwp in ret.WzProperties.Where(iwp => iwp.Name == t))
                {
                    ret = iwp;
                    foundChild = true;
                    break;
                }

                if (!foundChild)
                {
                    return null;
                }
            }

            return ret;
        }

        /// <summary>
        /// Dispose the object
        /// </summary>
        public override void Dispose()
        {
            Name = null;
            PngProperty.Dispose();
            PngProperty = null;
            foreach (var prop in _properties) prop.Dispose();

            _properties.Clear();
            _properties = null;
        }

        #endregion

        #region Custom Members

        /// <summary>
        /// The png image for this canvas property
        /// </summary>
        public WzPngProperty PngProperty { get; set; }

        /// <summary>
        /// Creates a WzCanvasProperty with the specified name
        /// </summary>
        /// <param name="name">The name of the property</param>
        public WzCanvasProperty(string name)
        {
            Name = name;
        }

        /// <summary>
        /// Adds a property to the property list of this property
        /// </summary>
        /// <param name="prop">The property to add</param>
        public void AddProperty(WzImageProperty prop)
        {
            prop.Parent = this;
            _properties.Add(prop);
        }

        public void AddProperties(IEnumerable<WzImageProperty> props)
        {
            foreach (var prop in props) AddProperty(prop);
        }

        /// <summary>
        /// Clears the list of properties
        /// </summary>
        public void ClearProperties()
        {
            foreach (var prop in _properties) prop.Parent = null;
            _properties.Clear();
        }

        #endregion

        #region Cast Values

        #endregion
    }
}