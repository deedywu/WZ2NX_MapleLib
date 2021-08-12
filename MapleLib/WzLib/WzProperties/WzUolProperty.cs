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

//uncomment to enable automatic UOL resolving, comment to disable it

#define UOLRES

using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Wz2Nx_MapleLib.MapleLib.WzLib.WzProperties
{
    /// <summary>
    /// A property that's value is a string
    /// </summary>
    public sealed class WzUolProperty : WzExtended
    {
        #region Fields

        private WzObject _linkVal;

        #endregion


        #region Inherited Members

        public object WzValue => LinkValue;

        /// <summary>
        /// The parent of the object
        /// </summary>
        public override WzObject Parent { get; internal set; }

        /// <summary>
        /// The name of the property
        /// </summary>
        public override string Name { get; set; }

#if UOLRES
        public override List<WzImageProperty> WzProperties =>
            LinkValue is WzImageProperty property ? property.WzProperties : null;


        public override WzImageProperty this[string name] =>
            LinkValue is WzImageProperty property ? property[name] :
            LinkValue is WzImage img ? img[name] : null;

        public override WzImageProperty GetFromPath(string path)
        {
            return LinkValue is WzImageProperty property ? property.GetFromPath(path) :
                LinkValue is WzImage img ? img.GetFromPath(path) : null;
        }
#endif

        /// <summary>
        /// The WzPropertyType of the property
        /// </summary>
        public override WzPropertyType PropertyType => WzPropertyType.Uol;

        /// <summary>
        /// Disposes the object
        /// </summary>
        public override void Dispose()
        {
            Name = null;
            Value = null;
        }

        #endregion

        #region Custom Members

        /// <summary>
        /// The value of the property
        /// </summary>
        public string Value { get; private set; }

#if UOLRES
        private WzObject LinkValue
        {
            get
            {
                if (_linkVal != null) return _linkVal;
                var paths = Value.Split('/');
                _linkVal = Parent;
                foreach (var path1 in paths)
                {
                    if (path1 == "..")
                    {
                        _linkVal = _linkVal.Parent;
                    }
                    else
                    {
                        if (_linkVal is WzImageProperty property1) _linkVal = property1[path1];
                        else if (_linkVal is WzImage image1) _linkVal = image1[path1];
                        else if (_linkVal is WzDirectory directory1) _linkVal = directory1[path1];
                        else
                        {   // cms wz has many errors,I had to fixed for only once  
                            paths = ("../" + Value).Split('/');
                            _linkVal = Parent;
                            foreach (var path2 in paths)
                            {
                                if (path2 == "..")
                                {
                                    _linkVal = _linkVal.Parent;
                                }
                                else
                                {
                                    if (_linkVal is WzImageProperty property2) _linkVal = property2[path2];
                                    else if (_linkVal is WzImage image2) _linkVal = image2[path2];
                                    else if (_linkVal is WzDirectory directory2) _linkVal = directory2[path2];
                                    else
                                    {
                                        Console.WriteLine("UOL got nexon'd at property: " + FullPath);
                                    }
                                }
                            }

                            break;
                        }
                    }
                }

                return _linkVal;
            }
        }
#endif

        /// <summary>
        /// Creates a WzUOLProperty with the specified name and value
        /// </summary>
        /// <param name="name">The name of the property</param>
        /// <param name="value">The value of the property</param>
        public WzUolProperty(string name, string value)
        {
            this.Name = name;
            this.Value = value;
        }

        #endregion

        #region Cast Values

#if UOLRES
        public override int GetInt()
        {
            return LinkValue.GetInt();
        }

        public override short GetShort()
        {
            return LinkValue.GetShort();
        }

        public override long GetLong()
        {
            return LinkValue.GetLong();
        }

        public override float GetFloat()
        {
            return LinkValue.GetFloat();
        }

        public override double GetDouble()
        {
            return LinkValue.GetDouble();
        }

        public override string GetString()
        {
            return LinkValue.GetString();
        }

        public override Vector2 Pos()
        {
            return LinkValue.Pos();
        }

        public override byte[] GetBytes()
        {
            return LinkValue.GetBytes();
        }
#else
        public override string GetString()
        {
            return val;
        }
#endif
        public override string ToString()
        {
            return Value;
        }

        #endregion
    }
}