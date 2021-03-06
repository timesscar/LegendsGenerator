﻿// -------------------------------------------------------------------------------------------------
// <copyright file="DictionaryPropertyNode.cs" company="Tom Luppi">
//     Copyright (c) Tom Luppi.  All rights reserved.
// </copyright>
// -------------------------------------------------------------------------------------------------

namespace LegendsGenerator.Editor.ContractParsing
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Windows;
    using LegendsGenerator.Contracts.Definitions;

    /// <summary>
    /// A node which is a dictionary.
    /// </summary>
    public class DictionaryPropertyNode : PropertyNode
    {
        /// <summary>
        /// The type of the dictionary value.
        /// </summary>
        private Type valueType;

        /// <summary>
        /// The element info.
        /// </summary>
        private ElementInfo info;

        /// <summary>
        /// The thing.
        /// </summary>
        private object? thing;

        /// <summary>
        /// Initializes a new instance of the <see cref="DictionaryPropertyNode"/> class.
        /// </summary>
        /// <param name="thing">The thing this node points to.</param>
        /// <param name="info">The property info.</param>
        /// <param name="options">The options for this node.</param>
        /// <param name="readOnly">If this instance should be read only.</param>
        public DictionaryPropertyNode(object? thing, ElementInfo info, IEnumerable<PropertyInfo> options, bool readOnly = false)
            : base(thing, info, options, readOnly)
        {
            this.valueType = info.PropertyType.GetGenericArguments().Last();
            this.info = info;
            this.thing = thing;

            this.GenerateNodes();
        }

        /// <inheritdoc/>
        public override bool CanCreate => true;

        /// <inheritdoc/>
        public override void HandleCreate(object sender, RoutedEventArgs e)
        {
            // validate that this key isn't a duplicate.
            foreach (var key in this.AsDictionary().Keys)
            {
                if (BaseDefinition.UnsetString.Equals(key))
                {
                    Console.WriteLine($"You must change the name of the {BaseDefinition.UnsetString} before adding another node..");
                    return;
                }
            }

            object? def;
            if (this.valueType == typeof(string))
            {
                def = BaseDefinition.UnsetString;
            }
            else
            {
                def = Activator.CreateInstance(this.valueType);
            }

            if (def == null)
            {
                throw new InvalidOperationException("A null instance was created.");
            }

            this.AsDictionary().Add(BaseDefinition.UnsetString, def);
            this.GenerateNodes();
        }

        /// <summary>
        /// Generates all lower nodes based on content dictionary.
        /// </summary>
        private void GenerateNodes()
        {
            this.Nodes.Clear();

            IDictionary dictionary = this.AsDictionary();

            foreach (DictionaryEntry? kvp in dictionary)
            {
                if (kvp == null)
                {
                    continue;
                }

                ElementInfo kvpInfo = new ElementInfo(
                    name: kvp.Value.Key as string ?? "UNKNOWN",
                    description: this.info.Description,
                    propertyType: this.valueType,
                    nullable: true, // Set nullable to true, if the value is set to null than the element will be deleted.
                    getValue: () => this.AsDictionary()[kvp.Value.Key],
                    setValue: value => this.HandleSetValue(kvp.Value.Key, value),
                    getCompiledParameters: this.info.GetCompiledParameters,
                    compiled: this.info.Compiled)
                {
                    ChangeName = newName => this.ChangeName((kvp.Value.Key as string)!, newName),
                    NameCreatesVariableName = true, // This is currently always true, should plumb in correctly with attribute for auto-magic.
                };

                PropertyNode? node = DefinitionParser.ToNode(this.thing, kvpInfo);
                if (node != null)
                {
                    this.AddNode(node);
                }
            }
        }

        /// <summary>
        /// Gets the contents as a dictionary.
        /// </summary>
        /// <exception cref="InvalidOperationException">The contents is not a dictionary.</exception>
        /// <returns>The contents, as dictionary.</returns>
        private IDictionary AsDictionary()
        {
            IDictionary? dictionary = this.Content as IDictionary;

            if (dictionary == null)
            {
                throw new InvalidOperationException($"Content type must be Dictionary, was {this.Content?.GetType().Name ?? "Null"}");
            }

            return dictionary;
        }

        /// <summary>
        /// Handles te value being set, deleting the entry if it's set to null.
        /// </summary>
        /// <param name="key">The dictionary key.</param>
        /// <param name="value">The new value.</param>
        private void HandleSetValue(object key, object? value)
        {
            if (value == null)
            {
                this.AsDictionary().Remove(key);
                this.GenerateNodes();
            }
            else
            {
                this.AsDictionary()[key] = value;
            }
        }

        /// <summary>
        /// Handles te change name scenario.
        /// </summary>
        /// <param name="oldName">The old name.</param>
        /// <param name="newName">The new name.</param>
        private void ChangeName(string oldName, string newName)
        {
            if (oldName == newName)
            {
                return;
            }

            IDictionary dictionary = this.AsDictionary();

            // validate that this key isn't a duplicate.
            foreach (var key in dictionary.Keys)
            {
                if (newName.Equals(key))
                {
                    Console.WriteLine($"A key with name {newName} already exists in the dictionary.");
                    return;
                }
            }

            object? entry = dictionary[oldName];
            dictionary.Remove(oldName);
            dictionary[newName] = entry;

            this.GenerateNodes();
        }
    }
}
