using AutoMapper;
using FPTU.Capstone.AMKCollective.Application.DTOs.Chat;
using FPTU.Capstone.AMKCollective.Application.Helpers;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Repositories;
using FPTU.Capstone.AMKCollective.Application.Interfaces.Services;
using FPTU.Capstone.AMKCollective.Application.Services;
using FPTU.Capstone.AMKCollective.Domain.Entities;
using FPTU.Capstone.AMKCollective.Domain.Enums;
using Moq;
using System.Linq;

namespace FPTU.Capstone.AMKCollective.Tests
{
    public class ChatServiceTests
    {
        private static ChatService CreateService(
            Mock<IUnitOfWork> unitOfWorkMock,
            Mock<IConversationRepository> conversationRepoMock,
            Mock<IUserRepository> userRepoMock,
            Mock<IMapper>? mapperMock = null,
            Mock<IChatRealtimePublisher>? realtimePublisherMock = null)
        {
            unitOfWorkMock.SetupGet(u => u.Conversations).Returns(conversationRepoMock.Object);
            unitOfWorkMock.SetupGet(u => u.Users).Returns(userRepoMock.Object);
            unitOfWorkMock.Setup(u => u.CommitAsync()).Returns(Task.CompletedTask);

            mapperMock ??= new Mock<IMapper>();
            mapperMock
                .Setup(m => m.Map<ChatMessageResponse>(It.IsAny<Message>()))
                .Returns<Message>(m => new ChatMessageResponse
                {
                    Id = m.Id,
                    SenderId = m.SenderId,
                    Content = m.Content,
                    MessageType = m.MessageType,
                    ParentMessageId = m.ParentMessageId,
                    CreatedAt = m.CreatedAt
                });

            realtimePublisherMock ??= new Mock<IChatRealtimePublisher>();
            realtimePublisherMock
                .Setup(p => p.PublishMessageReceivedAsync(It.IsAny<ChatMessageResponse>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            realtimePublisherMock
                .Setup(p => p.PublishReadReceiptAsync(It.IsAny<int>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            realtimePublisherMock
                .Setup(p => p.PublishReactionChangedAsync(It.IsAny<MessageReactionResponse>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            return new ChatService(unitOfWorkMock.Object, mapperMock.Object, realtimePublisherMock.Object);
        }

        private static void SetupConversationSummaryDependencies(
            Mock<IConversationRepository> conversationRepoMock,
            Mock<IUserRepository> userRepoMock,
            Guid currentUserId,
            int conversationId,
            Guid otherUserId,
            DateTime? updatedAt = null)
        {
            conversationRepoMock
                .Setup(r => r.GetConversationParticipantsAsync(conversationId))
                .ReturnsAsync(new List<UserConversation>
                {
                    new UserConversation { Id = 1, UserId = currentUserId, ConversationId = conversationId },
                    new UserConversation { Id = 2, UserId = otherUserId, ConversationId = conversationId }
                });

            userRepoMock
                .Setup(r => r.GetByIdAsync(otherUserId))
                .ReturnsAsync(new User
                {
                    Id = otherUserId,
                    FirstName = "Other",
                    LastName = "User",
                    Username = "otheruser",
                    Email = "other@test.com",
                    Image = "avatar.png"
                });

            var messageTime = updatedAt ?? DateTime.UtcNow;
            conversationRepoMock
                .Setup(r => r.GetMessagesByConversationIdAsync(conversationId))
                .ReturnsAsync(new List<Message>
                {
                    new Message { Id = 10, Content = "latest", CreatedAt = messageTime }
                });

            conversationRepoMock
                .Setup(r => r.GetUnreadCountAsync(currentUserId, conversationId))
                .ReturnsAsync(3);
        }

        [Fact]
        public async Task GetOrCreateDirectConversationAsync_WhenNotExists_CreatesConversationAndParticipants()
        {
            var currentUserId = Guid.NewGuid();
            var targetUserId = Guid.NewGuid();

            var targetUser = new User
            {
                Id = targetUserId,
                FirstName = "Target",
                LastName = "User",
                Email = "target@test.com",
                Username = "target"
            };

            var createdConversationId = 100;

            var unitOfWorkMock = new Mock<IUnitOfWork>();
            var conversationRepoMock = new Mock<IConversationRepository>();
            var userRepoMock = new Mock<IUserRepository>();

            userRepoMock.Setup(r => r.GetByIdAsync(targetUserId)).ReturnsAsync(targetUser);
            conversationRepoMock.Setup(r => r.GetConversationByUserIdsAsync(currentUserId, targetUserId))
                .ReturnsAsync((Conversation?)null);

            conversationRepoMock.Setup(r => r.AddConversationAsync(It.IsAny<Conversation>()))
                .Callback<Conversation>(c => c.Id = createdConversationId)
                .Returns(Task.CompletedTask);

            conversationRepoMock.Setup(r => r.AddUserConversationAsync(It.IsAny<UserConversation>()))
                .Returns(Task.CompletedTask);

            conversationRepoMock.Setup(r => r.GetConversationParticipantsAsync(createdConversationId))
                .ReturnsAsync(new List<UserConversation>
                {
                    new UserConversation { Id = 1, UserId = currentUserId, ConversationId = createdConversationId },
                    new UserConversation { Id = 2, UserId = targetUserId, ConversationId = createdConversationId }
                });

            conversationRepoMock.Setup(r => r.GetMessagesByConversationIdAsync(createdConversationId))
                .ReturnsAsync(new List<Message>());

            conversationRepoMock.Setup(r => r.GetUnreadCountAsync(currentUserId, createdConversationId))
                .ReturnsAsync(0);

            var service = CreateService(unitOfWorkMock, conversationRepoMock, userRepoMock);

            var result = await service.GetOrCreateDirectConversationAsync(currentUserId, targetUserId);

            Assert.Equal(createdConversationId, result.ConversationId);
            Assert.Equal(targetUserId, result.OtherUserId);
            Assert.Equal("Target User", result.OtherUserName);

            conversationRepoMock.Verify(r => r.AddConversationAsync(It.IsAny<Conversation>()), Times.Once);
            conversationRepoMock.Verify(r => r.AddUserConversationAsync(It.IsAny<UserConversation>()), Times.Exactly(2));
            unitOfWorkMock.Verify(u => u.CommitAsync(), Times.Exactly(2));
        }

        [Fact]
        public async Task GetOrCreateDirectConversationAsync_WhenTargetIsCurrentUser_ThrowsInvalidOperationException()
        {
            var currentUserId = Guid.NewGuid();

            var service = CreateService(new Mock<IUnitOfWork>(), new Mock<IConversationRepository>(), new Mock<IUserRepository>());

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.GetOrCreateDirectConversationAsync(currentUserId, currentUserId));
        }

        [Fact]
        public async Task GetOrCreateDirectConversationAsync_WhenTargetNotFound_ThrowsKeyNotFoundException()
        {
            var currentUserId = Guid.NewGuid();
            var targetUserId = Guid.NewGuid();

            var unitOfWorkMock = new Mock<IUnitOfWork>();
            var conversationRepoMock = new Mock<IConversationRepository>();
            var userRepoMock = new Mock<IUserRepository>();

            userRepoMock.Setup(r => r.GetByIdAsync(targetUserId)).ReturnsAsync((User?)null);

            var service = CreateService(unitOfWorkMock, conversationRepoMock, userRepoMock);

            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                service.GetOrCreateDirectConversationAsync(currentUserId, targetUserId));
        }

        [Fact]
        public async Task GetOrCreateDirectConversationAsync_WhenExists_DoesNotCreateNewConversation()
        {
            var currentUserId = Guid.NewGuid();
            var targetUserId = Guid.NewGuid();
            const int conversationId = 77;

            var unitOfWorkMock = new Mock<IUnitOfWork>();
            var conversationRepoMock = new Mock<IConversationRepository>();
            var userRepoMock = new Mock<IUserRepository>();

            userRepoMock.Setup(r => r.GetByIdAsync(targetUserId)).ReturnsAsync(new User
            {
                Id = targetUserId,
                FirstName = "Target",
                LastName = "User",
                Username = "target",
                Email = "target@test.com"
            });

            conversationRepoMock
                .Setup(r => r.GetConversationByUserIdsAsync(currentUserId, targetUserId))
                .ReturnsAsync(new Conversation { Id = conversationId, UpdatedAt = DateTime.UtcNow });

            SetupConversationSummaryDependencies(conversationRepoMock, userRepoMock, currentUserId, conversationId, targetUserId);

            var service = CreateService(unitOfWorkMock, conversationRepoMock, userRepoMock);

            var result = await service.GetOrCreateDirectConversationAsync(currentUserId, targetUserId);

            Assert.Equal(conversationId, result.ConversationId);
            Assert.Equal(targetUserId, result.OtherUserId);

            conversationRepoMock.Verify(r => r.AddConversationAsync(It.IsAny<Conversation>()), Times.Never);
            conversationRepoMock.Verify(r => r.AddUserConversationAsync(It.IsAny<UserConversation>()), Times.Never);
        }

        [Fact]
        public async Task SendMessageAsync_WithConversationId_PersistsMessageAndRecipients()
        {
            var senderId = Guid.NewGuid();
            var otherUserId = Guid.NewGuid();
            const int conversationId = 200;

            var unitOfWorkMock = new Mock<IUnitOfWork>();
            var conversationRepoMock = new Mock<IConversationRepository>();
            var userRepoMock = new Mock<IUserRepository>();

            conversationRepoMock.Setup(r => r.GetUserConversationAsync(senderId, conversationId))
                .ReturnsAsync(new UserConversation { Id = 11, UserId = senderId, ConversationId = conversationId });

            conversationRepoMock.Setup(r => r.AddMessageAsync(It.IsAny<Message>()))
                .Callback<Message>(m => m.Id = 321)
                .Returns(Task.CompletedTask);

            conversationRepoMock.Setup(r => r.GetConversationParticipantsAsync(conversationId))
                .ReturnsAsync(new List<UserConversation>
                {
                    new UserConversation { Id = 11, UserId = senderId, ConversationId = conversationId },
                    new UserConversation { Id = 12, UserId = otherUserId, ConversationId = conversationId }
                });

            conversationRepoMock.Setup(r => r.AddMessageRecipientsAsync(It.IsAny<IEnumerable<MessageRecipient>>()))
                .Returns(Task.CompletedTask);

            conversationRepoMock.Setup(r => r.GetConversationByIdAsync(conversationId))
                .ReturnsAsync(new Conversation { Id = conversationId });

            var service = CreateService(unitOfWorkMock, conversationRepoMock, userRepoMock);

            var result = await service.SendMessageAsync(senderId, new SendMessageRequest
            {
                ConversationId = conversationId,
                Content = "Hello world",
                MessageType = MediaType.Text
            });

            Assert.Equal(321, result.Id);
            Assert.Equal(conversationId, result.ConversationId);
            Assert.Equal(senderId, result.SenderId);
            Assert.Equal("Hello world", result.Content);

            conversationRepoMock.Verify(r => r.AddMessageAsync(It.IsAny<Message>()), Times.Once);
            conversationRepoMock.Verify(r => r.AddMessageRecipientsAsync(It.Is<IEnumerable<MessageRecipient>>(x => x.Count() == 2)), Times.Once);
            conversationRepoMock.Verify(r => r.UpdateConversation(It.IsAny<Conversation>()), Times.Once);
            unitOfWorkMock.Verify(u => u.CommitAsync(), Times.Exactly(2));
        }

        [Fact]
        public async Task SendMessageAsync_WhenContentEmpty_ThrowsInvalidOperationException()
        {
            var senderId = Guid.NewGuid();

            var service = CreateService(new Mock<IUnitOfWork>(), new Mock<IConversationRepository>(), new Mock<IUserRepository>());

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.SendMessageAsync(senderId, new SendMessageRequest
                {
                    ConversationId = 1,
                    Content = "   ",
                    MessageType = MediaType.Text
                }));
        }

        [Fact]
        public async Task SendMessageAsync_WhenConversationAndTargetMissing_ThrowsInvalidOperationException()
        {
            var senderId = Guid.NewGuid();

            var service = CreateService(new Mock<IUnitOfWork>(), new Mock<IConversationRepository>(), new Mock<IUserRepository>());

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.SendMessageAsync(senderId, new SendMessageRequest
                {
                    Content = "hello",
                    MessageType = MediaType.Text
                }));
        }

        [Fact]
        public async Task SendMessageAsync_WhenNotConversationMember_ThrowsUnauthorizedAccessException()
        {
            var senderId = Guid.NewGuid();

            var unitOfWorkMock = new Mock<IUnitOfWork>();
            var conversationRepoMock = new Mock<IConversationRepository>();
            var userRepoMock = new Mock<IUserRepository>();

            conversationRepoMock
                .Setup(r => r.GetUserConversationAsync(senderId, 123))
                .ReturnsAsync((UserConversation?)null);

            var service = CreateService(unitOfWorkMock, conversationRepoMock, userRepoMock);

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                service.SendMessageAsync(senderId, new SendMessageRequest
                {
                    ConversationId = 123,
                    Content = "hello",
                    MessageType = MediaType.Text
                }));
        }

        [Fact]
        public async Task SendMessageAsync_WhenParentMessageNotFound_ThrowsKeyNotFoundException()
        {
            var senderId = Guid.NewGuid();

            var unitOfWorkMock = new Mock<IUnitOfWork>();
            var conversationRepoMock = new Mock<IConversationRepository>();
            var userRepoMock = new Mock<IUserRepository>();

            conversationRepoMock
                .Setup(r => r.GetUserConversationAsync(senderId, 88))
                .ReturnsAsync(new UserConversation { Id = 1, UserId = senderId, ConversationId = 88 });

            conversationRepoMock
                .Setup(r => r.GetMessageByIdAsync(999))
                .ReturnsAsync((Message?)null);

            var service = CreateService(unitOfWorkMock, conversationRepoMock, userRepoMock);

            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                service.SendMessageAsync(senderId, new SendMessageRequest
                {
                    ConversationId = 88,
                    ParentMessageId = 999,
                    Content = "reply",
                    MessageType = MediaType.Text
                }));
        }

        [Fact]
        public async Task SendMessageAsync_WhenParentMessageOutsideConversation_ThrowsInvalidOperationException()
        {
            var senderId = Guid.NewGuid();

            var unitOfWorkMock = new Mock<IUnitOfWork>();
            var conversationRepoMock = new Mock<IConversationRepository>();
            var userRepoMock = new Mock<IUserRepository>();

            conversationRepoMock
                .Setup(r => r.GetUserConversationAsync(senderId, 88))
                .ReturnsAsync(new UserConversation { Id = 1, UserId = senderId, ConversationId = 88 });

            conversationRepoMock
                .Setup(r => r.GetMessageByIdAsync(1000))
                .ReturnsAsync(new Message { Id = 1000, CreatedAt = DateTime.UtcNow });

            conversationRepoMock
                .Setup(r => r.IsMessageInConversationAsync(1000, 88))
                .ReturnsAsync(false);

            var service = CreateService(unitOfWorkMock, conversationRepoMock, userRepoMock);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.SendMessageAsync(senderId, new SendMessageRequest
                {
                    ConversationId = 88,
                    ParentMessageId = 1000,
                    Content = "reply",
                    MessageType = MediaType.Text
                }));
        }

        [Fact]
        public async Task GetConversationMessagesAsync_WhenNotMember_ThrowsUnauthorizedAccessException()
        {
            var userId = Guid.NewGuid();

            var unitOfWorkMock = new Mock<IUnitOfWork>();
            var conversationRepoMock = new Mock<IConversationRepository>();
            var userRepoMock = new Mock<IUserRepository>();

            conversationRepoMock
                .Setup(r => r.GetUserConversationAsync(userId, 55))
                .ReturnsAsync((UserConversation?)null);

            var service = CreateService(unitOfWorkMock, conversationRepoMock, userRepoMock);

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                service.GetConversationMessagesAsync(userId, 55, null, 20));
        }

        [Fact]
        public async Task GetConversationMessagesAsync_WhenHasMore_ReturnsAscendingPageAndNextCursor()
        {
            var userId = Guid.NewGuid();
            var conversationId = 66;
            var baseTime = new DateTime(2026, 01, 01, 0, 0, 0, DateTimeKind.Utc);

            var messages = new List<Message>
            {
                new Message { Id = 1, SenderId = userId, Content = "m1", MessageType = MediaType.Text, CreatedAt = baseTime.AddMinutes(1) },
                new Message { Id = 2, SenderId = userId, Content = "m2", MessageType = MediaType.Text, CreatedAt = baseTime.AddMinutes(2) },
                new Message { Id = 3, SenderId = userId, Content = "m3", MessageType = MediaType.Text, CreatedAt = baseTime.AddMinutes(3) },
                new Message { Id = 4, SenderId = userId, Content = "m4", MessageType = MediaType.Text, CreatedAt = baseTime.AddMinutes(4) }
            };

            var unitOfWorkMock = new Mock<IUnitOfWork>();
            var conversationRepoMock = new Mock<IConversationRepository>();
            var userRepoMock = new Mock<IUserRepository>();

            conversationRepoMock
                .Setup(r => r.GetUserConversationAsync(userId, conversationId))
                .ReturnsAsync(new UserConversation { Id = 1, UserId = userId, ConversationId = conversationId });

            conversationRepoMock
                .Setup(r => r.GetMessagesByConversationIdAsync(conversationId))
                .ReturnsAsync(messages);

            var service = CreateService(unitOfWorkMock, conversationRepoMock, userRepoMock);

            var result = await service.GetConversationMessagesAsync(userId, conversationId, null, 2);

            Assert.Equal(2, result.Items.Count());
            Assert.True(result.HasMore);
            Assert.NotNull(result.NextCursor);

            var returnedIds = result.Items.Select(i => i.Id).ToList();
            Assert.Equal(new List<int> { 3, 4 }, returnedIds);

            var decoded = CursorHelper.DecodeCursor(result.NextCursor);
            Assert.True(decoded.HasValue);
            Assert.Equal(3, decoded.Value.Id);
        }

        [Fact]
        public async Task GetUserConversationsAsync_WhenHasMore_ReturnsPageAndCursor()
        {
            var userId = Guid.NewGuid();
            var otherUserA = Guid.NewGuid();
            var otherUserB = Guid.NewGuid();
            var otherUserC = Guid.NewGuid();

            var t1 = new DateTime(2026, 01, 01, 10, 0, 0, DateTimeKind.Utc);
            var t2 = t1.AddMinutes(1);
            var t3 = t1.AddMinutes(2);

            var memberships = new List<UserConversation>
            {
                new UserConversation { UserId = userId, ConversationId = 101, Conversation = new Conversation { Id = 101, CreatedAt = t1, UpdatedAt = t1 } },
                new UserConversation { UserId = userId, ConversationId = 102, Conversation = new Conversation { Id = 102, CreatedAt = t2, UpdatedAt = t2 } },
                new UserConversation { UserId = userId, ConversationId = 103, Conversation = new Conversation { Id = 103, CreatedAt = t3, UpdatedAt = t3 } }
            };

            var unitOfWorkMock = new Mock<IUnitOfWork>();
            var conversationRepoMock = new Mock<IConversationRepository>();
            var userRepoMock = new Mock<IUserRepository>();

            conversationRepoMock
                .Setup(r => r.GetUserConversationsAsync(userId))
                .ReturnsAsync(memberships);

            conversationRepoMock
                .Setup(r => r.GetConversationParticipantsAsync(It.IsAny<int>()))
                .ReturnsAsync((int cId) =>
                {
                    var otherId = cId switch
                    {
                        101 => otherUserA,
                        102 => otherUserB,
                        _ => otherUserC
                    };

                    return new List<UserConversation>
                    {
                        new UserConversation { Id = 1, UserId = userId, ConversationId = cId },
                        new UserConversation { Id = 2, UserId = otherId, ConversationId = cId }
                    };
                });

            userRepoMock
                .Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync((Guid id) => new User
                {
                    Id = id,
                    FirstName = "Other",
                    LastName = "User",
                    Username = "other",
                    Email = "other@test.com"
                });

            conversationRepoMock
                .Setup(r => r.GetMessagesByConversationIdAsync(It.IsAny<int>()))
                .ReturnsAsync(new List<Message>());

            conversationRepoMock
                .Setup(r => r.GetUnreadCountAsync(userId, It.IsAny<int>()))
                .ReturnsAsync(0);

            var service = CreateService(unitOfWorkMock, conversationRepoMock, userRepoMock);

            var result = await service.GetUserConversationsAsync(userId, null, 2);

            Assert.Equal(2, result.Items.Count());
            Assert.True(result.HasMore);
            Assert.NotNull(result.NextCursor);
            Assert.Equal(new[] { 103, 102 }, result.Items.Select(x => x.ConversationId).ToArray());
        }

        [Fact]
        public async Task MarkMessagesAsReadAsync_WhenUnreadExists_UpdatesRecipients()
        {
            var userId = Guid.NewGuid();
            const int conversationId = 300;
            const int upToMessageId = 500;

            var recipients = new List<MessageRecipient>
            {
                new MessageRecipient { Id = 1, UserId = userId, MessageId = 490, IsRead = false },
                new MessageRecipient { Id = 2, UserId = userId, MessageId = 500, IsRead = false }
            };

            var unitOfWorkMock = new Mock<IUnitOfWork>();
            var conversationRepoMock = new Mock<IConversationRepository>();
            var userRepoMock = new Mock<IUserRepository>();

            conversationRepoMock.Setup(r => r.GetUserConversationAsync(userId, conversationId))
                .ReturnsAsync(new UserConversation { Id = 21, UserId = userId, ConversationId = conversationId });

            conversationRepoMock.Setup(r => r.GetUnreadRecipientsAsync(userId, conversationId, upToMessageId))
                .ReturnsAsync(recipients);

            var service = CreateService(unitOfWorkMock, conversationRepoMock, userRepoMock);

            await service.MarkMessagesAsReadAsync(userId, conversationId, upToMessageId);

            Assert.All(recipients, r => Assert.True(r.IsRead));
            conversationRepoMock.Verify(r => r.UpdateMessageRecipients(It.Is<IEnumerable<MessageRecipient>>(x => x.Count() == 2)), Times.Once);
            unitOfWorkMock.Verify(u => u.CommitAsync(), Times.Once);
        }

        [Fact]
        public async Task MarkMessagesAsReadAsync_WhenNoUnread_DoesNotCommit()
        {
            var userId = Guid.NewGuid();
            const int conversationId = 12;

            var unitOfWorkMock = new Mock<IUnitOfWork>();
            var conversationRepoMock = new Mock<IConversationRepository>();
            var userRepoMock = new Mock<IUserRepository>();

            conversationRepoMock
                .Setup(r => r.GetUserConversationAsync(userId, conversationId))
                .ReturnsAsync(new UserConversation { Id = 1, UserId = userId, ConversationId = conversationId });

            conversationRepoMock
                .Setup(r => r.GetUnreadRecipientsAsync(userId, conversationId, 50))
                .ReturnsAsync(new List<MessageRecipient>());

            var service = CreateService(unitOfWorkMock, conversationRepoMock, userRepoMock);

            await service.MarkMessagesAsReadAsync(userId, conversationId, 50);

            conversationRepoMock.Verify(r => r.UpdateMessageRecipients(It.IsAny<IEnumerable<MessageRecipient>>()), Times.Never);
            unitOfWorkMock.Verify(u => u.CommitAsync(), Times.Never);
        }

        [Fact]
        public async Task SetMessageReactionAsync_WhenMessageNotFound_ThrowsKeyNotFoundException()
        {
            var userId = Guid.NewGuid();
            const int conversationId = 501;
            const int messageId = 600;

            var unitOfWorkMock = new Mock<IUnitOfWork>();
            var conversationRepoMock = new Mock<IConversationRepository>();
            var userRepoMock = new Mock<IUserRepository>();

            conversationRepoMock
                .Setup(r => r.GetUserConversationAsync(userId, conversationId))
                .ReturnsAsync(new UserConversation { Id = 1, UserId = userId, ConversationId = conversationId });

            conversationRepoMock
                .Setup(r => r.GetMessageRecipientAsync(userId, conversationId, messageId))
                .ReturnsAsync((MessageRecipient?)null);

            var service = CreateService(unitOfWorkMock, conversationRepoMock, userRepoMock);

            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                service.SetMessageReactionAsync(userId, conversationId, messageId, MessageReaction.Love));
        }

        [Fact]
        public async Task SetMessageReactionAsync_WhenReactionNull_UnreactsAndCommits()
        {
            var userId = Guid.NewGuid();
            const int conversationId = 701;
            const int messageId = 702;

            var recipient = new MessageRecipient
            {
                Id = 1,
                UserId = userId,
                MessageId = messageId,
                MessageReaction = MessageReaction.Haha,
                IsRead = true
            };

            var unitOfWorkMock = new Mock<IUnitOfWork>();
            var conversationRepoMock = new Mock<IConversationRepository>();
            var userRepoMock = new Mock<IUserRepository>();

            conversationRepoMock
                .Setup(r => r.GetUserConversationAsync(userId, conversationId))
                .ReturnsAsync(new UserConversation { Id = 1, UserId = userId, ConversationId = conversationId });

            conversationRepoMock
                .Setup(r => r.GetMessageRecipientAsync(userId, conversationId, messageId))
                .ReturnsAsync(recipient);

            var service = CreateService(unitOfWorkMock, conversationRepoMock, userRepoMock);

            var result = await service.SetMessageReactionAsync(userId, conversationId, messageId, null);

            Assert.Null(result.Reaction);
            Assert.Null(recipient.MessageReaction);
            conversationRepoMock.Verify(r => r.UpdateMessageRecipient(recipient), Times.Once);
            unitOfWorkMock.Verify(u => u.CommitAsync(), Times.Once);
        }

        [Fact]
        public async Task GetConversationMessagesAsync_WithCursorAndSameTimestamp_UsesIdTieBreakCorrectly()
        {
            var userId = Guid.NewGuid();
            const int conversationId = 88;
            var sameTime = new DateTime(2026, 02, 01, 8, 0, 0, DateTimeKind.Utc);

            var messages = new List<Message>
            {
                new Message { Id = 1, SenderId = userId, Content = "m1", MessageType = MediaType.Text, CreatedAt = sameTime },
                new Message { Id = 2, SenderId = userId, Content = "m2", MessageType = MediaType.Text, CreatedAt = sameTime },
                new Message { Id = 3, SenderId = userId, Content = "m3", MessageType = MediaType.Text, CreatedAt = sameTime },
                new Message { Id = 4, SenderId = userId, Content = "m4", MessageType = MediaType.Text, CreatedAt = sameTime.AddMinutes(1) }
            };

            var unitOfWorkMock = new Mock<IUnitOfWork>();
            var conversationRepoMock = new Mock<IConversationRepository>();
            var userRepoMock = new Mock<IUserRepository>();

            conversationRepoMock
                .Setup(r => r.GetUserConversationAsync(userId, conversationId))
                .ReturnsAsync(new UserConversation { Id = 1, UserId = userId, ConversationId = conversationId });

            conversationRepoMock
                .Setup(r => r.GetMessagesByConversationIdAsync(conversationId))
                .ReturnsAsync(messages);

            var service = CreateService(unitOfWorkMock, conversationRepoMock, userRepoMock);
            var cursor = CursorHelper.EncodeCursor(sameTime, 2);

            var result = await service.GetConversationMessagesAsync(userId, conversationId, cursor, 10);

            var ids = result.Items.Select(i => i.Id).ToList();
            Assert.Equal(new List<int> { 1 }, ids);
        }

        [Fact]
        public async Task GetConversationMessagesAsync_WithInvalidCursor_TreatsAsFirstPage()
        {
            var userId = Guid.NewGuid();
            const int conversationId = 90;
            var baseTime = new DateTime(2026, 02, 01, 9, 0, 0, DateTimeKind.Utc);

            var messages = new List<Message>
            {
                new Message { Id = 1, SenderId = userId, Content = "m1", MessageType = MediaType.Text, CreatedAt = baseTime },
                new Message { Id = 2, SenderId = userId, Content = "m2", MessageType = MediaType.Text, CreatedAt = baseTime.AddMinutes(1) }
            };

            var unitOfWorkMock = new Mock<IUnitOfWork>();
            var conversationRepoMock = new Mock<IConversationRepository>();
            var userRepoMock = new Mock<IUserRepository>();

            conversationRepoMock
                .Setup(r => r.GetUserConversationAsync(userId, conversationId))
                .ReturnsAsync(new UserConversation { Id = 1, UserId = userId, ConversationId = conversationId });
            conversationRepoMock
                .Setup(r => r.GetMessagesByConversationIdAsync(conversationId))
                .ReturnsAsync(messages);

            var service = CreateService(unitOfWorkMock, conversationRepoMock, userRepoMock);

            var result = await service.GetConversationMessagesAsync(userId, conversationId, "bad_cursor", 30);

            Assert.Equal(2, result.Items.Count());
            Assert.False(result.HasMore);
        }

        [Fact]
        public async Task GetConversationMessagesAsync_WhenPageSizeInvalid_UsesDefault30()
        {
            var userId = Guid.NewGuid();
            const int conversationId = 91;
            var start = new DateTime(2026, 02, 02, 10, 0, 0, DateTimeKind.Utc);

            var messages = Enumerable.Range(1, 35)
                .Select(i => new Message
                {
                    Id = i,
                    SenderId = userId,
                    Content = $"m{i}",
                    MessageType = MediaType.Text,
                    CreatedAt = start.AddSeconds(i)
                })
                .ToList();

            var unitOfWorkMock = new Mock<IUnitOfWork>();
            var conversationRepoMock = new Mock<IConversationRepository>();
            var userRepoMock = new Mock<IUserRepository>();

            conversationRepoMock
                .Setup(r => r.GetUserConversationAsync(userId, conversationId))
                .ReturnsAsync(new UserConversation { Id = 1, UserId = userId, ConversationId = conversationId });
            conversationRepoMock
                .Setup(r => r.GetMessagesByConversationIdAsync(conversationId))
                .ReturnsAsync(messages);

            var service = CreateService(unitOfWorkMock, conversationRepoMock, userRepoMock);

            var result = await service.GetConversationMessagesAsync(userId, conversationId, null, 0);

            Assert.Equal(30, result.Items.Count());
            Assert.True(result.HasMore);
        }

        [Fact]
        public async Task GetUserConversationsAsync_WhenPageSizeAboveMax_UsesMax50()
        {
            var userId = Guid.NewGuid();
            var now = new DateTime(2026, 02, 02, 11, 0, 0, DateTimeKind.Utc);

            var memberships = Enumerable.Range(1, 55)
                .Select(i => new UserConversation
                {
                    UserId = userId,
                    ConversationId = i,
                    Conversation = new Conversation
                    {
                        Id = i,
                        CreatedAt = now.AddMinutes(i),
                        UpdatedAt = now.AddMinutes(i)
                    }
                })
                .ToList();

            var unitOfWorkMock = new Mock<IUnitOfWork>();
            var conversationRepoMock = new Mock<IConversationRepository>();
            var userRepoMock = new Mock<IUserRepository>();

            conversationRepoMock
                .Setup(r => r.GetUserConversationsAsync(userId))
                .ReturnsAsync(memberships);

            conversationRepoMock
                .Setup(r => r.GetConversationParticipantsAsync(It.IsAny<int>()))
                .ReturnsAsync((int cid) => new List<UserConversation>
                {
                    new UserConversation { Id = 1, UserId = userId, ConversationId = cid },
                    new UserConversation { Id = 2, UserId = Guid.NewGuid(), ConversationId = cid }
                });

            userRepoMock
                .Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync((Guid uid) => new User
                {
                    Id = uid,
                    FirstName = "Other",
                    LastName = "User",
                    Email = "other@test.com",
                    Username = "other"
                });

            conversationRepoMock
                .Setup(r => r.GetMessagesByConversationIdAsync(It.IsAny<int>()))
                .ReturnsAsync(new List<Message>());

            conversationRepoMock
                .Setup(r => r.GetUnreadCountAsync(userId, It.IsAny<int>()))
                .ReturnsAsync(0);

            var service = CreateService(unitOfWorkMock, conversationRepoMock, userRepoMock);

            var result = await service.GetUserConversationsAsync(userId, null, 999);

            Assert.Equal(50, result.Items.Count());
            Assert.True(result.HasMore);
            Assert.NotNull(result.NextCursor);
        }

        [Fact]
        public async Task SetMessageReactionAsync_WhenUserNotInConversation_ThrowsUnauthorizedAccessException()
        {
            var userId = Guid.NewGuid();
            const int conversationId = 333;
            const int messageId = 444;

            var unitOfWorkMock = new Mock<IUnitOfWork>();
            var conversationRepoMock = new Mock<IConversationRepository>();
            var userRepoMock = new Mock<IUserRepository>();

            conversationRepoMock
                .Setup(r => r.GetUserConversationAsync(userId, conversationId))
                .ReturnsAsync((UserConversation?)null);

            var service = CreateService(unitOfWorkMock, conversationRepoMock, userRepoMock);

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                service.SetMessageReactionAsync(userId, conversationId, messageId, MessageReaction.Angry));
        }

        [Fact]
        public async Task IsUserInConversationAsync_ReturnsTrueOrFalse_BasedOnMembership()
        {
            var userId = Guid.NewGuid();

            var unitOfWorkMock = new Mock<IUnitOfWork>();
            var conversationRepoMock = new Mock<IConversationRepository>();
            var userRepoMock = new Mock<IUserRepository>();

            conversationRepoMock
                .Setup(r => r.GetUserConversationAsync(userId, 1))
                .ReturnsAsync(new UserConversation { Id = 1, UserId = userId, ConversationId = 1 });

            conversationRepoMock
                .Setup(r => r.GetUserConversationAsync(userId, 2))
                .ReturnsAsync((UserConversation?)null);

            var service = CreateService(unitOfWorkMock, conversationRepoMock, userRepoMock);

            var inConversation = await service.IsUserInConversationAsync(userId, 1);
            var notInConversation = await service.IsUserInConversationAsync(userId, 2);

            Assert.True(inConversation);
            Assert.False(notInConversation);
        }
    }
}
