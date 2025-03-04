﻿using FakeXrmEasy.Abstractions;
using FakeXrmEasy.Abstractions.Exceptions;
using FakeXrmEasy.Abstractions.FakeMessageExecutors;
using FakeXrmEasy.Core.Exceptions.Query;
using FakeXrmEasy.Extensions;
using FakeXrmEasy.Extensions.FetchXml;
using FakeXrmEasy.Query;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace FakeXrmEasy.Middleware.Crud.FakeMessageExecutors
{
    /// <summary>
    /// 
    /// </summary>
    public class RetrieveMultipleRequestExecutor : IFakeMessageExecutor
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public bool CanExecute(OrganizationRequest request)
        {
            return request is RetrieveMultipleRequest;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="req"></param>
        /// <param name="ctx"></param>
        /// <returns></returns>
        public OrganizationResponse Execute(OrganizationRequest req, IXrmFakedContext ctx)
        {
            var context = ctx as XrmFakedContext;
            var request = req as RetrieveMultipleRequest;
            List<Entity> list = null;
            PagingInfo pageInfo = null;
            QueryExpression qe;

            string entityName = null;

            if (request.Query is QueryExpression)
            {
                qe = (request.Query as QueryExpression).Clone();
                entityName = qe.EntityName;

                var linqQuery = qe.ToQueryable(context);
                list = linqQuery.ToList();
            }
            else if (request.Query is FetchExpression)
            {
                var fetchXml = (request.Query as FetchExpression).Query;
                XDocument xmlDoc = fetchXml.ToXmlDocument();
                qe = fetchXml.ToQueryExpression(context);
                entityName = qe.EntityName;

                var linqQuery = qe.ToQueryable(context);
                list = linqQuery.ToList();

                if (xmlDoc.HasAggregations())
                {
                    list = list.Aggregate(context, xmlDoc);
                }
            }
            else if (request.Query is QueryByAttribute)
            {
                // We instantiate a QueryExpression to be executed as we have the implementation done already
                var query = request.Query as QueryByAttribute;
                qe = new QueryExpression(query.EntityName);
                entityName = qe.EntityName;

                qe.ColumnSet = query.ColumnSet;
                qe.Criteria = new FilterExpression();
                for (var i = 0; i < query.Attributes.Count; i++)
                {
                    qe.Criteria.AddCondition(new ConditionExpression(query.Attributes[i], ConditionOperator.Equal, query.Values[i]));
                }

                foreach (var order in query.Orders)
                {
                    qe.AddOrder(order.AttributeName, order.OrderType);
                }

                qe.PageInfo = query.PageInfo;
                qe.TopCount = query.TopCount;

                // QueryExpression now done... execute it!
                var linqQuery = qe.ToQueryable(context);
                list = linqQuery.ToList();
            }
            else
            {
                throw UnsupportedExceptionFactory.NotImplementedOrganizationRequest(ctx.LicenseContext.Value, request.Query.GetType());
            }

            if (qe.Distinct)
            {
                list = GetDistinctEntities(list);
            }

            // Handle the top count before taking paging into account
            if (qe.TopCount != null && qe.TopCount.Value < list.Count)
            {
                list = list.Take(qe.TopCount.Value).ToList();
            }

            // Handle TotalRecordCount here?
            int totalRecordCount = -1;
            if (qe?.PageInfo?.ReturnTotalRecordCount == true)
            {
                totalRecordCount = list.Count;
            }

            // Handle paging
            var pageSize = context.MaxRetrieveCount;
            pageInfo = qe.PageInfo;
            int pageNumber = 1;
            if (pageInfo != null && pageInfo.PageNumber > 0)
            {
                pageNumber = pageInfo.PageNumber;
                pageSize = pageInfo.Count == 0 ? context.MaxRetrieveCount : pageInfo.Count;
            }

            // Figure out where in the list we need to start and how many items we need to grab
            int numberToGet = pageSize;
            int startPosition = 0;

            if (pageNumber != 1)
            {
                startPosition = (pageNumber - 1) * pageSize;
            }

            if (list.Count < pageSize)
            {
                numberToGet = list.Count;
            }
            else if (list.Count - pageSize * (pageNumber - 1) < pageSize)
            {
                numberToGet = list.Count - (pageSize * (pageNumber - 1));
            }

            var recordsToReturn = startPosition + numberToGet > list.Count ? new List<Entity>() : list.GetRange(startPosition, numberToGet);

            recordsToReturn.ForEach(e => e.ApplyDateBehaviour(context));
            recordsToReturn.ForEach(e => PopulateFormattedValues(e));

            var response = new RetrieveMultipleResponse
            {
                Results = new ParameterCollection
                                 {
                                    { "EntityCollection", new EntityCollection(recordsToReturn) }
                                 }
            };
            response.EntityCollection.EntityName = entityName;
            response.EntityCollection.MoreRecords = (list.Count - pageSize * pageNumber) > 0;
            response.EntityCollection.TotalRecordCount = totalRecordCount;

            if (response.EntityCollection.MoreRecords)
            {
                var first = response.EntityCollection.Entities.First();
                var last = response.EntityCollection.Entities.Last();
                response.EntityCollection.PagingCookie = $"<cookie page=\"{pageNumber}\"><{first.LogicalName}id last=\"{last.Id.ToString("B").ToUpper()}\" first=\"{first.Id.ToString("B").ToUpper()}\" /></cookie>";
            }

            return response;
        }

        /// <summary>
        /// Populates the formatted values property of this entity record based on the proxy types
        /// </summary>
        /// <param name="e"></param>
        protected void PopulateFormattedValues(Entity e)
        {
            // Iterate through attributes and retrieve formatted values based on type
            foreach (var attKey in e.Attributes.Keys)
            {
                var value = e[attKey];
                string formattedValue = "";
                if (!e.FormattedValues.ContainsKey(attKey) && (value != null))
                {
                    bool bShouldAdd;
                    formattedValue = this.GetFormattedValueForValue(value, out bShouldAdd);
                    if (bShouldAdd)
                    {
                        e.FormattedValues.Add(attKey, formattedValue);
                    }
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        /// <param name="bShouldAddFormattedValue"></param>
        /// <returns></returns>
        protected string GetFormattedValueForValue(object value, out bool bShouldAddFormattedValue)
        {
            bShouldAddFormattedValue = false;
            var sFormattedValue = string.Empty;

            if (value is Enum)
            {
                // Retrieve the enum type
                sFormattedValue = Enum.GetName(value.GetType(), value);
                bShouldAddFormattedValue = true;
            }
            else if (value is AliasedValue)
            {
                return this.GetFormattedValueForValue((value as AliasedValue)?.Value, out bShouldAddFormattedValue);
            }

            return sFormattedValue;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public Type GetResponsibleRequestType()
        {
            return typeof(RetrieveMultipleRequest);
        }

        private static List<Entity> GetDistinctEntities(IEnumerable<Entity> input)
        {
            var output = new List<Entity>();

            foreach (var entity in input)
            {
                if (!output.Any(i => i.LogicalName == entity.LogicalName && i.Attributes.SequenceEqual(entity.Attributes)))
                {
                    output.Add(entity);
                }
            }

            return output;
        }
    }
}