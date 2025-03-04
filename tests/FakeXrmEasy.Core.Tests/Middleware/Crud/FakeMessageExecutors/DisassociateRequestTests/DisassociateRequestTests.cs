﻿using Crm;
using Microsoft.Xrm.Sdk;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using FakeXrmEasy.Abstractions;

namespace FakeXrmEasy.Tests.Middleware.Crud.FakeMessageExecutors.DisassociateRequestTests
{
    public class DisassociateRequestTests: FakeXrmEasyTestsBase
    {

        [Fact]
        public void When_execute_is_called_with_reverse_param_order()
        {

            var userId = Guid.NewGuid();
            var teamId = Guid.NewGuid();
            var user2Id = Guid.NewGuid();
            _context.Initialize(new List<Entity> {
                new SystemUser
                {
                    Id = userId
                },
                new SystemUser
                {
                    Id = user2Id
                },
                new Team
                {
                    Id = teamId
                },
                new Entity("teammembership")
                {
                    Id = Guid.NewGuid(),
                    ["systemuserid"] = userId,
                    ["teamid"] = teamId
                },
                new Entity("teammembership")
                {
                    Id = Guid.NewGuid(),
                    ["systemuserid"] =teamId ,
                    ["teamid"] = userId
                },
                new Entity("teammembership")
                {
                    Id = Guid.NewGuid(),
                    ["systemuserid"] = user2Id,
                    ["teamid"] = teamId
                },
                new Entity("teammembership")
                {
                    Id = Guid.NewGuid(),
                    ["systemuserid"] =teamId ,
                    ["teamid"] = user2Id
                }
            });

            _context.AddRelationship("teammembership", new XrmFakedRelationship()
            {
                RelationshipType = XrmFakedRelationship.FakeRelationshipType.ManyToMany,
                IntersectEntity = "teammembership",
                Entity1Attribute = "systemuserid",
                Entity1LogicalName = "systemuser",
                Entity2Attribute = "teamid",
                Entity2LogicalName = "team"
            });

            _service.Disassociate("team", teamId, new Relationship("teammembership"),
                new EntityReferenceCollection(new List<EntityReference> { new EntityReference("systemuser", userId), new EntityReference("systemuser", user2Id) }));

            using (Crm.XrmServiceContext ctx = new XrmServiceContext(_service))
            {
                var correctAssociation1 = (from tu in ctx.TeamMembershipSet
                                           where tu.TeamId == teamId
                                           && tu.SystemUserId == user2Id
                                           select tu).ToList();
                Assert.False(correctAssociation1.Any());

                var correctAssociation = (from tu in ctx.TeamMembershipSet
                                          where tu.TeamId == teamId
                                          && tu.SystemUserId == userId
                                          select tu).ToList();
                Assert.False(correctAssociation.Any());

                var wrongAssociation2 = (from tu in ctx.TeamMembershipSet
                                         where tu.TeamId == user2Id
                                         && tu.SystemUserId == teamId
                                         select tu).ToList();
                Assert.Single(wrongAssociation2);

                var wrongAssociation = (from tu in ctx.TeamMembershipSet
                                        where tu.TeamId == userId
                                        && tu.SystemUserId == teamId
                                        select tu).ToList();
                Assert.Single(wrongAssociation);
            }
        }

        [Fact]
        public void When_execute_is_called_with_same_as_relationnship_param_order()
        {
            

            var user2Id = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var teamId = Guid.NewGuid();
            var team2Id = Guid.NewGuid();
            _context.Initialize(new List<Entity> {
                new SystemUser
                {
                    Id = userId
                },
                new SystemUser
                {
                    Id = user2Id
                },
                new Team
                {
                    Id = teamId
                },
                new Team
                {
                    Id = team2Id
                },
                new Entity("teammembership")
                {
                    Id = Guid.NewGuid(),
                    ["systemuserid"] = userId,
                    ["teamid"] = teamId
                },
                new Entity("teammembership")
                {
                    Id = Guid.NewGuid(),
                    ["systemuserid"] =teamId ,
                    ["teamid"] = userId
                },
                new Entity("teammembership")
                {
                    Id = Guid.NewGuid(),
                    ["systemuserid"] = userId,
                    ["teamid"] = team2Id
                },
                new Entity("teammembership")
                {
                    Id = Guid.NewGuid(),
                    ["systemuserid"] =team2Id ,
                    ["teamid"] = userId
                }
            });

            _context.AddRelationship("teammembership", new XrmFakedRelationship()
            {
                RelationshipType = XrmFakedRelationship.FakeRelationshipType.ManyToMany,
                IntersectEntity = "teammembership",
                Entity1Attribute = "systemuserid",
                Entity1LogicalName = "systemuser",
                Entity2Attribute = "teamid",
                Entity2LogicalName = "team"
            });

            
            _service.Disassociate("systmuser", userId, new Relationship("teammembership"),
                new EntityReferenceCollection(new List<EntityReference> { new EntityReference("team", teamId), new EntityReference("team", team2Id) }));

            using (Crm.XrmServiceContext ctx = new XrmServiceContext(_service))
            {
                var correctAssociation1 = (from tu in ctx.TeamMembershipSet
                                           where tu.TeamId == teamId
                                           && tu.SystemUserId == userId
                                           select tu).ToList();
                Assert.False(correctAssociation1.Any());

                var correctAssociation = (from tu in ctx.TeamMembershipSet
                                          where tu.TeamId == team2Id
                                          && tu.SystemUserId == userId
                                          select tu).ToList();
                Assert.False(correctAssociation.Any());

                var wrongAssociation1 = (from tu in ctx.TeamMembershipSet
                                         where tu.TeamId == userId
                                         && tu.SystemUserId == team2Id
                                         select tu).ToList();
                Assert.Single(wrongAssociation1);

                var wrongAssociation = (from tu in ctx.TeamMembershipSet
                                        where tu.TeamId == userId
                                        && tu.SystemUserId == teamId
                                        select tu).ToList();
                Assert.Single(wrongAssociation);
            }
        }
    }
}