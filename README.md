# This library provides deep/shallow cloning of the source object and return its copy

The following types are cloneable:

* System types: **bool**, **char**, **int**, **long**, **float**, **double**, **string**
* Any instance of **object** type
* Any non-system **ValueType**
* Any reference type having a **new X()**-style constructor, i.e. a constructor accepting zero arguments
* Any one-dimensional **array** of items having one of types listed above
* Any **ICollection<T>** with **new X()**-style constructor, where **T** is any of types listed above

Any public field / property of these types are cloned in accordance with the [Cloneable] attribute options:

* **[Cloneable(CloningMode.Ignore)]** indicates it shouldn't be cloned
* **[Cloneable(CloningMode.Deep)]** indicates it should be deep-cloned
* **[Cloneable(CloningMode.Shallow)]** indicates the field should be copied as-is, i.e. if it's a reference type, the reference should be copied, and if it's a value type, it should be shallow cloned
* no **[Cloneable]** attribute: it should be deep-cloned.