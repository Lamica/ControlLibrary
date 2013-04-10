﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace ControlLibrary
{
    internal class DataSourceItem : IDataSourceItem
    {
        internal bool isChecked;
        internal WeakEventHandler<PropertyChangedEventArgs> propertyChangedHandler;
        private object value;
        private int index;
        private IDataSourceGroup parentGroup;
        private MatListSource owner;
        private IDataSourceItem next;
        private IDataSourceItem previous;

        public DataSourceItem(MatListSource owner, object value)
        {
            this.owner = owner;
            this.value = value;
            this.HookPropertyChanged();
        }

        /// <summary>
        /// Gets a boolean value determining whether the data item
        /// will appear as checked when the list control shows
        /// check boxes next to each visual item.
        /// </summary>
        public bool IsChecked
        {
            get
            {
                return this.isChecked;
            }
        }

        /// <summary>
        /// Gets the raw data associated with this item.
        /// </summary>
        public object Value
        {
            get
            {
                if (this.value == MatListSource.UnsetObject)
                {
                    object data = this.owner.RequestDataForItem(this);

                    if (data != MatListSource.UnsetObject)
                    {
                        this.value = data;
                    }
                }
                return this.value;
            }
        }

        /// <summary>
        /// Gets or sets the <see cref="IDataSourceGroup"/> instance that owns this item.
        /// </summary>
        public IDataSourceGroup ParentGroup
        {
            get
            {
                return this.parentGroup;
            }
            set
            {
                this.parentGroup = value;
            }
        }

        public object DisplayValue
        {
            get;
            set;
        }

        public IDataSourceItem Next
        {
            get
            {
                return this.next;
            }
            set
            {
                this.next = value;
            }
        }

        public IDataSourceItem Previous
        {
            get
            {
                return this.previous;
            }
            set
            {
                this.previous = value;
            }
        }

        public int Index
        {
            get
            {
                return this.index;
            }
            set
            {
                this.index = value;
            }
        }

        public void HookPropertyChanged()
        {
            INotifyPropertyChanged propertyChanged = this.value as INotifyPropertyChanged;
            if (propertyChanged != null)
            {
                this.propertyChangedHandler = new WeakEventHandler<PropertyChangedEventArgs>(propertyChanged, this.owner, MatListSource.PropertyChangedEventName);
            }
        }

        public void UnhookPropertyChanged()
        {
            if (this.propertyChangedHandler != null)
            {
                this.propertyChangedHandler.Unsubscribe();
                this.propertyChangedHandler = null;
            }
        }

        bool IDataSourceItem.ChangeValue(object value)
        {
            this.value = value;
            return true;
        }
    }
}
