﻿using System;
using System.Collections.Generic;
#if NUGET
using Microsoft.Diagnostics.Tracing;
#else
using System.Diagnostics.Tracing;
#endif
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

#if NUGET
namespace EventSourceProxy.NuGet
#else
namespace EventSourceProxy
#endif
{
	/// <summary>
	/// Provides the Event attributes when generating proxy classes.
	/// The default implementation uses EventAttribute and EventExceptionAttribute to generate the values.
	/// </summary>
	public class EventAttributeProvider
	{
		/// <summary>
		/// Initializes a new instance of the EventAttributeProvider class.
		/// </summary>
		public EventAttributeProvider() : this(EventLevel.Informational, EventLevel.Error)
		{
		}

		/// <summary>
		/// Initializes a new instance of the EventAttributeProvider class.
		/// </summary>
		/// <param name="eventLevel">The default EventLevel for events if not specified by EventAttributes.</param>
		/// <param name="exceptionEventLevel">The default exception events if not specified by EventExceptionAttributes.</param>
		public EventAttributeProvider(EventLevel eventLevel, EventLevel exceptionEventLevel)
		{
			EventLevel = eventLevel;
			ExceptionEventLevel = exceptionEventLevel;
		}

		/// <summary>
		/// Gets the default EventLevel for methods.
		/// </summary>
		public EventLevel EventLevel { get; private set; }

		/// <summary>
		/// Gets the default EventLevel for exceptions generated by methods.
		/// </summary>
		public EventLevel ExceptionEventLevel { get; private set; }

		/// <summary>
		/// Returns an EventAttribute for the given call context.
		/// </summary>
		/// <param name="context">The context of the call.</param>
		/// <param name="nextEventId">The next event ID to use if not specified by some other mechanism.</param>
		/// <param name="parameterMapping">The parameter mapping for the method, or null if not a method call.</param>
		/// <returns>The EventAttribute for the call context.</returns>
		public virtual EventAttribute GetEventAttribute(InvocationContext context, int nextEventId, IReadOnlyCollection<ParameterMapping> parameterMapping)
		{
			if (context == null) throw new ArgumentNullException("context");

			EventAttribute eventAttribute = context.MethodInfo.GetCustomAttribute<EventAttribute>();
			if (eventAttribute != null)
				return eventAttribute;

			return new EventAttribute(nextEventId)
			{
				Level = GetEventLevelForContext(context, null),
				Message = GetEventMessage(context, parameterMapping)
			};
		}

		/// <summary>
		/// Returns an EventAttribute for the Completed or Faulted events for a call context.
		/// </summary>
		/// <param name="baseAttribute">The EventAttribute for the method call that should be copied.</param>
		/// <param name="context">The context of the call.</param>
		/// <param name="nextEventId">The next event ID to use if not specified by some other mechanism.</param>
		/// <returns>The EventAttribute for the call context.</returns>
		public virtual EventAttribute CopyEventAttribute(EventAttribute baseAttribute, InvocationContext context, int nextEventId)
		{
			if (baseAttribute == null) throw new ArgumentNullException("baseAttribute");
			if (context == null) throw new ArgumentNullException("context");

			return new EventAttribute(nextEventId)
			{
				Keywords = baseAttribute.Keywords,
				Level = GetEventLevelForContext(context, baseAttribute),
				Message = GetEventMessage(context, null),
				Opcode = baseAttribute.Opcode,
				Task = baseAttribute.Task,
				Version = baseAttribute.Version
			};
		}

		/// <summary>
		/// Gets the appropriate EventLevel for the call context.
		/// </summary>
		/// <param name="context">The context of the call.</param>
		/// <param name="baseAttribute">The base attribute to copy if there are no additional attributes.</param>
		/// <returns>The EventLevel for the call context.</returns>
		protected virtual EventLevel GetEventLevelForContext(InvocationContext context, EventAttribute baseAttribute)
		{
			if (context == null) throw new ArgumentNullException("context");

			// for faulted methods, allow the EventExceptionAttribute to override the event level
			if (context.ContextType == InvocationContextTypes.MethodFaulted)
			{
				var attribute = context.MethodInfo.GetCustomAttribute<EventExceptionAttribute>();
				if (attribute != null)
					return attribute.Level;

				attribute = context.MethodInfo.DeclaringType.GetCustomAttribute<EventExceptionAttribute>();
				if (attribute != null)
					return attribute.Level;

				return ExceptionEventLevel;
			}

			// check for an attribute on the type
			var implementationAttribute = context.MethodInfo.DeclaringType.GetCustomAttribute<EventSourceImplementationAttribute>();
			if (implementationAttribute != null && implementationAttribute.Level.HasValue)
				return implementationAttribute.Level.Value;

			if (baseAttribute != null)
				return baseAttribute.Level;

			return EventLevel;
		}

		/// <summary>
		/// Gets the message for an event.
		/// </summary>
		/// <param name="context">The context of the call.</param>
		/// <param name="parameterMapping">The parameter mapping for the method, or null if not a method call.</param>
		/// <returns>The message for the event.</returns>
		protected virtual string GetEventMessage(InvocationContext context, IReadOnlyCollection<ParameterMapping> parameterMapping)
		{
			if (context == null) throw new ArgumentNullException("context");

			switch (context.ContextType)
			{
				case InvocationContextTypes.MethodCall:
					if (parameterMapping == null) throw new ArgumentNullException("parameterMapping");
					return String.Join(" ", Enumerable.Range(0, parameterMapping.Count).Select(i => String.Format(CultureInfo.InvariantCulture, "{{{0}}}", i)));

				case InvocationContextTypes.MethodFaulted:
					return "{0}";

				case InvocationContextTypes.MethodCompletion:
					if (context.MethodInfo.ReturnType != typeof(void))
						return "{0}";
					else
						return String.Empty;
			}

			return String.Empty;
		}
	}
}
