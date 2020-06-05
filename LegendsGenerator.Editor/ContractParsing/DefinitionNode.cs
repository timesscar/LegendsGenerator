﻿// -------------------------------------------------------------------------------------------------
// <copyright file="DefinitionNode.cs" company="Tom Luppi">
//     Copyright (c) Tom Luppi.  All rights reserved.
// </copyright>
// -------------------------------------------------------------------------------------------------

namespace LegendsGenerator.Editor.ContractParsing
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.ComponentModel;
    using System.Linq;
    using System.Reflection;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Media;

    using LegendsGenerator.Contracts.Definitions.Validation;

    /// <summary>
    /// Represents a node of the contract.
    /// </summary>
    public abstract class DefinitionNode : INotifyPropertyChanged
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DefinitionNode"/> class.
        /// </summary>
        /// <param name="thing">The thing this node points to.</param>
        /// <param name="property">The property info.</param>
        /// <param name="options">The options for this node.</param>
        /// <param name="readOnly">If this instance should be read only.</param>
        public DefinitionNode(
            object? thing,
            ElementInfo property,
            IEnumerable<PropertyInfo> options,
            bool readOnly = false)
        {
            this.Name = property.Name.Split("_").Last();
            this.Description = property.Description;
            this.Nullable = property.Nullable;
            this.ContentsModifiable = !readOnly;

            this.GetContentsFunc = property.GetMethod;

            if (!readOnly)
            {
                this.SetContentsFunc = property.SetMethod;
            }

            foreach (PropertyInfo option in options)
            {
                DefinitionNode? node = DefinitionParser.ToNode(thing, option);
                if (node != null)
                {
                    this.Options.Add(node);
                }
            }

            // Set up that changes to Content or underlying nodes causes validation changes.
            this.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName?.Equals(nameof(this.Content)) ?? false)
                {
                    this.OnPropertyChanged(nameof(this.ValidationFailures));
                }
            };

            foreach (DefinitionNode node in this.Nodes)
            {
                node.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName?.Equals(nameof(node.ValidationFailures)) ?? false)
                    {
                        this.OnPropertyChanged(nameof(this.ValidationFailures));
                    }
                };
            }

            foreach (DefinitionNode node in this.Options)
            {
                node.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName?.Equals(nameof(node.ValidationFailures)) ?? false)
                    {
                        this.OnPropertyChanged(nameof(this.ValidationFailures));
                    }
                };
            }

            this.Nodes.CollectionChanged += this.NodeListItemsChanged;

            this.Options.CollectionChanged += this.NodeListItemsChanged;
        }

        /// <summary>
        /// Notifies when a property is changed.
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Gets or sets the name of the node, typically either the property name or the dictionary key.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets the description of this node.
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// Gets a value indicating whether this node can be set to null.
        /// </summary>
        public bool Nullable { get; }

        /// <summary>
        /// Gets or sets the contents of the node; null if there's no contents beyond sub nodes.
        /// </summary>
        public object? Content
        {
            get
            {
                return this.GetContentsFunc?.Invoke();
            }

            set
            {
                if (!this.ContentsModifiable)
                {
                    throw new InvalidOperationException("Can not modify contents");
                }

                if (this.SetContentsFunc == null)
                {
                    throw new InvalidOperationException($"{nameof(this.SetContentsFunc)} msut be attached to set contents.");
                }

                this.SetContentsFunc.Invoke(value);
                this.OnPropertyChanged(nameof(this.Content));
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether this node can be renamed.
        /// </summary>
        public bool Renamable { get; protected set; }

        /// <summary>
        /// Gets or sets a value indicating whether the contents are modifiable.
        /// </summary>
        public bool ContentsModifiable { get; protected set; }

        /// <summary>
        /// Gets or sets a value indicating whether the subnodes can be modified.
        /// </summary>
        public bool SubNodesModifiable { get; protected set; }

        /// <summary>
        /// Gets the list of additional options which can be added to this node.
        /// </summary>
        public ObservableCollection<DefinitionNode> Options { get; } = new ObservableCollection<DefinitionNode>();

        /// <summary>
        /// Gets the list of subnodes on this node.
        /// </summary>
        public ObservableCollection<DefinitionNode> Nodes { get; } = new ObservableCollection<DefinitionNode>();

        /// <summary>
        /// Gets the list of validation failures on this node.
        /// </summary>
        public IList<ValidationIssue> ValidationFailures
        {
            get
            {
                List<ValidationIssue> failures = new List<ValidationIssue>();
                failures.AddRange(this.GetLevelIssues());
                this.Options.ToList().ForEach(node => failures.AddRange(node.ValidationFailures.Select(v => v.Clone(node.Name))));
                this.Nodes.ToList().ForEach(node => failures.AddRange(node.ValidationFailures.Select(v => v.Clone(node.Name))));
                return failures;
            }
        }

        /// <summary>
        /// Gets the text color to use for the title.
        /// </summary>
        public Brush GetTextColor
        {
            get
            {
                if (this.ValidationFailures.Any(v => v.Level == ValidationLevel.Info))
                {
                    return Brushes.Blue;
                }
                else if (this.ValidationFailures.Any(v => v.Level == ValidationLevel.Warning))
                {
                    return Brushes.Orange;
                }
                else if (this.ValidationFailures.Any(v => v.Level == ValidationLevel.Error))
                {
                    return Brushes.Red;
                }
                else
                {
                    return Brushes.Black;
                }
            }
        }

        /// <summary>
        /// Gets or sets a function which returns the contents of this node.
        /// </summary>
        protected Func<object?>? GetContentsFunc { get; set; }

        /// <summary>
        /// Gets or sets a function which sets the contents of this node.
        /// </summary>
        protected Action<object?>? SetContentsFunc { get; set; }

        /// <summary>
        /// Renames this instance.
        /// </summary>
        /// <param name="name">The new name to apply.</param>
        public virtual void Rename(string name)
        {
            throw new NotImplementedException($"{nameof(this.Rename)} is not implemented.");
        }

        /// <summary>
        /// gets the UI element to display as the content.
        /// </summary>
        /// <returns>The UI element.</returns>
        public virtual UIElement GetContentElement()
        {
            return new TextBlock()
            {
                Text = this.Content?.ToString(),
            };
        }

        /// <summary>
        /// Converts an action to be an object action.
        /// </summary>
        /// <typeparam name="T">The typical input type.</typeparam>
        /// <param name="action">The action to convert.</param>
        /// <returns>The action, which now takes object? as input.</returns>
        protected static Action<object?> ConvertAction<T>(Action<T> action)
        {
            return new Action<object?>(input =>
            {
                if (input is T asT)
                {
                    action(asT);
                }
                else
                {
                    throw new InvalidOperationException($"Must set as {typeof(T).Name}.");
                }
            });
        }

        /// <summary>
        /// Gets the issues present on this specific level of definition.
        /// </summary>
        /// <returns>The list of issues on this level.</returns>
        protected virtual IEnumerable<ValidationIssue> GetLevelIssues()
        {
            return Array.Empty<ValidationIssue>();
        }

        /// <summary>
        /// Fires property changed events.
        /// </summary>
        /// <param name="propertyName">True when property changed.</param>
        protected void OnPropertyChanged(string propertyName)
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

            // If the vlidation changed, than the text color changed as well.
            if (propertyName.Equals(nameof(this.ValidationFailures)))
            {
                this.OnPropertyChanged(nameof(this.GetTextColor));
            }
        }

        /// <summary>
        /// Action preformed when a node list changes.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The event.</param>
        private void NodeListItemsChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            this.OnPropertyChanged(nameof(this.ValidationFailures));

            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add && e.NewItems != null)
            {
                foreach (DefinitionNode node in e.NewItems.OfType<DefinitionNode>())
                {
                    node.PropertyChanged += (s, e) =>
                    {
                        if (e.PropertyName?.Equals(nameof(node.ValidationFailures)) ?? false)
                        {
                            this.OnPropertyChanged(nameof(this.ValidationFailures));
                        }
                    };
                }
            }
        }
    }
}
