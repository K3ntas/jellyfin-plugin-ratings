/**
 * Jellyfin Ratings Plugin - Client-side component
 */

(function () {
    'use strict';

    const RatingsPlugin = {
        pluginId: 'a1b2c3d4-e5f6-4a5b-8c9d-0e1f2a3b4c5d',
        ratingsCache: {}, // Cache for card ratings to avoid duplicate API calls
        currentLanguage: 'en', // Default language
        validLanguages: ['en', 'es', 'zh', 'pt', 'ru', 'ja', 'de', 'fr', 'ko', 'it', 'tr', 'pl', 'nl', 'ar', 'hi', 'lt'], // Supported languages
        badgeDisplayProfiles: [], // Resolution-based badge display profiles
        ratingsEnabled: true, // Whether ratings feature is enabled (loaded from config)

        // Chat state
        chatEnabled: false, // Whether chat feature is enabled (loaded from config)
        chatConfig: null, // Chat configuration from server
        chatOpen: false, // Whether chat window is open
        chatMessages: [], // Current chat messages
        chatUsers: [], // Online users
        chatPollingInterval: null, // Polling timer
        chatLastMessageId: null, // Last seen message ID
        chatUnreadCount: 0, // Unread message count
        chatTypingUsers: [], // Users currently typing
        chatIsAdmin: false, // Current user is admin
        chatIsModerator: false, // Current user is moderator
        chatBanStatus: null, // Current user's ban status
        emojiCategories: {
            smileys: ['😀', '😃', '😄', '😁', '😆', '😅', '🤣', '😂', '🙂', '😊', '😇', '🥰', '😍', '🤩', '😘', '😗', '😚', '😙', '🥲', '😋', '😛', '😜', '🤪', '😝', '🤑', '🤗', '🤭', '🤫', '🤔', '🤐', '🤨', '😐', '😑', '😶', '😏', '😒', '🙄', '😬', '🤥', '😌', '😔', '😪', '🤤', '😴', '😷', '🤒', '🤕', '🤢', '🤮', '🤧', '🥵', '🥶', '🥴', '😵', '🤯', '🤠', '🥳', '🥸', '😎', '🤓', '🧐'],
            gestures: ['👍', '👎', '👊', '✊', '🤛', '🤜', '👏', '🙌', '👐', '🤲', '🤝', '🙏', '✌️', '🤞', '🤟', '🤘', '🤙', '👋', '🖐️', '✋', '🖖', '👌', '🤌', '🤏', '✍️', '🤳', '💪', '🦾', '🦿', '🦵', '🦶', '👂', '🦻', '👃', '🧠', '🫀', '🫁', '🦷', '🦴', '👀', '👁️', '👅', '👄'],
            hearts: ['❤️', '🧡', '💛', '💚', '💙', '💜', '🖤', '🤍', '🤎', '💔', '❣️', '💕', '💞', '💓', '💗', '💖', '💘', '💝', '💟'],
            objects: ['🎉', '🎊', '🎁', '🎈', '🎄', '🎃', '🎂', '🍰', '🧁', '🍪', '🍩', '🍦', '☕', '🍵', '🍺', '🍻', '🥂', '🍾', '🎮', '🎲', '🎯', '🎳', '⚽', '🏀', '🏈', '⚾', '🎾', '🏐', '🎱', '🏓', '🏸', '🏒', '🥊', '🛹', '🎿', '⛷️', '🏂', '🏋️', '🤸', '🤼', '🤽', '🧗', '🚴', '🏍️', '🚗', '✈️', '🚀', '🛸']
        },

        // Supported languages: en, es, zh, pt, ru, ja, de, fr, ko, it, tr, pl, nl, ar, hi, lt
        translations: {
            en: {
                requestMedia: 'Request Media', manageRequests: 'Manage Media Requests', requestDescription: '📬 Request Your Favorite Media!',
                requestDescriptionText: 'Use this form to request movies or TV series that you\'d like to watch. The admin will review your request and add it to the library as soon as possible.',
                mediaTitle: 'Media Title *', mediaTitlePlaceholder: 'e.g., Breaking Bad, The Godfather', type: 'Type *', selectType: '-- Select Type --',
                movie: 'Movie', tvSeries: 'TV Series', anime: 'Anime', documentary: 'Documentary', other: 'Other',
                additionalNotes: 'Additional Notes', notesPlaceholder: 'Season number, year, specific details, etc.', submitRequest: 'Submit Request',
                yourRequests: 'Your Requests', loadingRequests: 'Loading your requests...', noRequests: 'You haven\'t requested any media yet',
                errorLoading: 'Error loading your requests', notSpecified: 'Not specified', noDetails: 'No details', watchNow: '🎬 Watch Now',
                noRequestsYet: 'No media requests yet', newRequest: 'New Request', pending: 'PENDING', processing: 'PROCESSING', done: 'DONE', rejected: 'REJECTED',
                titleRequired: 'Please enter a media title', typeRequired: 'Please select a media type', requestSubmitted: 'Request submitted successfully!',
                requestFailed: 'Failed to submit request', statusUpdated: 'Status updated', statusUpdateFailed: 'Failed to update status',
                addLink: '+ Link', enterMediaLink: 'Enter media link:', delete: 'Delete', confirmDelete: 'Are you sure you want to delete this request?',
                mediaLinkPlaceholder: 'Media link (paste URL when done)', unknown: 'Unknown', loading: 'Loading...',
                snooze: 'Snooze', unsnooze: 'Unsnooze', snoozed: 'SNOOZED', snoozedUntil: 'Snoozed until', snoozeDate: 'Snooze until date',
                categoryNew: '🆕 New', categoryProcessing: '🔄 Processing', categoryPending: '⏳ Pending', categorySnoozed: '💤 Snoozed', categoryDone: '✅ Done', categoryRejected: '❌ Rejected',
                createRequest: 'Create Request', latestMedia: 'Latest Media', latestMediaLoading: 'Loading...', latestMediaEmpty: 'No recent media found', latestMediaError: 'Failed to load',
                newEpisode: '+1 episode', newEpisodes: '+{count} episodes', typeMovie: 'Movie', typeSeries: 'Series', typeAnime: 'Anime', typeOther: 'Other',
                timeAgo: 'ago', timeJustNow: 'just now', timeMinutes: 'min', timeHours: 'h', timeDays: 'd',
                mediaManagement: 'Media', mediaManagementTitle: 'Media Management', mediaSearch: 'Search...', mediaTypeAll: 'All Types', mediaTypeMovie: 'Movies', mediaTypeSeries: 'Series',
                mediaSortBy: 'Sort by', mediaSortTitle: 'Title', mediaSortYear: 'Year', mediaSortRating: 'Rating', mediaSortPlays: 'Plays', mediaSortSize: 'Size',
                mediaSortDateAdded: 'Date Added', mediaSortPlayCount: 'Plays', mediaSortWatchTime: 'Watch Time', mediaLoading: 'Loading media...', mediaNoResults: 'No media found', mediaError: 'Error loading media',
                mediaScheduleDelete: 'Schedule Delete', mediaCancelDelete: 'Cancel Deletion', mediaDeleteIn: 'Delete in', mediaLeavingIn: 'Leaving in',
                media1Day: '1 Day', media3Days: '3 Days', media1Week: '1 Week', media2Weeks: '2 Weeks', mediaCustom: 'Custom...', mediaCustomHours: 'Hours',
                mediaSchedule: 'Schedule', mediaCancel: 'Cancel', mediaNoScheduled: 'No scheduled deletions', mediaScheduledBy: 'Scheduled By', mediaDeletesIn: 'Deletes In',
                mediaActions: 'Actions', mediaChange: 'Change', mediaChangeTime: 'Change deletion time', mediaSoon: 'Soon', mediaDays: 'days', mediaPlays: 'plays',
                mediaMinutes: 'min', mediaGB: 'GB', mediaMB: 'MB', mediaPage: 'Page', mediaOf: 'of', mediaPrev: 'Prev', mediaNext: 'Next', mediaGo: 'Go',
                mediaTypeScheduled: 'Scheduled', mediaSettings: 'Settings', mediaIncludeTypes: 'Include media types:', mediaTypesHint: 'Select which media types to show',
                requestDeleteRequest: 'Request to delete request', requestDeleteMedia: 'Request to delete media', deletionRequests: 'Deletion Requests', noDeletionRequests: 'No deletion requests yet',
                deleteNow: 'Delete ~1h', schedule1Day: '1 Day', schedule1Week: '1 Week', schedule1Month: '1 Month', rejectDeletion: 'Reject', approveDeleteRequest: 'Approve',
                alreadyRequested: 'Deletion Requested', deletionApproved: 'APPROVED', deletionRejected: 'REJECTED', deletionPending: 'PENDING',
                deletionRequestSent: 'Deletion request sent!', deletionRequestFailed: 'Failed to send deletion request', deletionActionFailed: 'Failed to process deletion action',
                deleteRequest: 'Delete Request', deleteMedia: 'Delete Media', rejectionReasonPrompt: 'Enter rejection reason (optional):', rejectionReasonLabel: 'Reason:',
                deletionLimitReached: 'Maximum deletion requests reached for this item', banUser: 'Ban', unbanUser: 'Unban', bannedUsers: 'Banned Users',
                ban1Day: '1 Day', ban1Week: '1 Week', ban1Month: '1 Month', banPermanent: 'Permanent', banExpires: 'Expires:', banPermanentLabel: 'Permanent',
                bannedBy: 'by', noBannedUsers: 'No banned users', youAreBanned: 'You are banned from this action', banSuccess: 'User banned successfully', unbanSuccess: 'User unbanned successfully',
                // Chat translations
                liveChat: 'Live Chat', chatOnline: 'online', chatTyping: 'is typing...', chatTypingMultiple: 'are typing...',
                chatSend: 'Send', chatPlaceholder: 'Type a message...', chatNoMessages: 'No messages yet. Start the conversation!',
                chatSearchGif: 'Search GIFs...', chatTrending: 'Trending', chatPoweredBy: 'Powered by Tenor',
                chatDeleted: 'Message deleted', chatYou: 'You', chatJustNow: 'Just now', chatMinutesAgo: 'm ago', chatHoursAgo: 'h ago', chatYesterday: 'Yesterday',
                chatModerators: 'Moderators', chatAddMod: 'Add Moderator', chatRemoveMod: 'Remove', chatBannedUsers: 'Banned Users',
                chatBan: 'Ban', chatUnban: 'Unban', chatMute: 'Mute', chatBanFromMedia: 'Ban from Media', chatMinutes: 'minutes',
                chatBanned: 'You are banned from chat', chatMuted: 'You are temporarily muted', chatRateLimited: 'Slow down! Too many messages.',
                chatConnecting: 'Connecting...', chatReconnecting: 'Reconnecting...', chatDisconnected: 'Disconnected',
                chatClearAll: 'Clear All', chatClearConfirm: 'Delete all chat messages? This cannot be undone.', chatCleared: 'Chat cleared'
            },
            es: {
                requestMedia: 'Solicitar Contenido', manageRequests: 'Gestionar Solicitudes', requestDescription: '📬 ¡Solicita tu Contenido Favorito!',
                requestDescriptionText: 'Usa este formulario para solicitar películas o series que te gustaría ver. El administrador revisará tu solicitud.',
                mediaTitle: 'Título *', mediaTitlePlaceholder: 'ej., Breaking Bad, El Padrino', type: 'Tipo *', selectType: '-- Seleccionar Tipo --',
                movie: 'Película', tvSeries: 'Serie de TV', anime: 'Anime', documentary: 'Documental', other: 'Otro',
                additionalNotes: 'Notas Adicionales', notesPlaceholder: 'Número de temporada, año, detalles específicos, etc.', submitRequest: 'Enviar Solicitud',
                yourRequests: 'Tus Solicitudes', loadingRequests: 'Cargando tus solicitudes...', noRequests: 'No has solicitado ningún contenido aún',
                errorLoading: 'Error al cargar tus solicitudes', notSpecified: 'No especificado', noDetails: 'Sin detalles', watchNow: '🎬 Ver Ahora',
                noRequestsYet: 'No hay solicitudes aún', newRequest: 'Nueva Solicitud', pending: 'PENDIENTE', processing: 'PROCESANDO', done: 'COMPLETADO', rejected: 'RECHAZADO',
                titleRequired: 'Por favor ingresa un título', typeRequired: 'Por favor selecciona un tipo', requestSubmitted: '¡Solicitud enviada exitosamente!',
                requestFailed: 'Error al enviar solicitud', statusUpdated: 'Estado actualizado', statusUpdateFailed: 'Error al actualizar estado',
                addLink: '+ Enlace', enterMediaLink: 'Ingresa el enlace:', delete: 'Eliminar', confirmDelete: '¿Estás seguro de eliminar esta solicitud?',
                mediaLinkPlaceholder: 'Enlace del contenido', unknown: 'Desconocido', loading: 'Cargando...',
                snooze: 'Posponer', unsnooze: 'Reactivar', snoozed: 'POSPUESTO', snoozedUntil: 'Pospuesto hasta', snoozeDate: 'Posponer hasta fecha',
                categoryNew: '🆕 Nuevo', categoryProcessing: '🔄 Procesando', categoryPending: '⏳ Pendiente', categorySnoozed: '💤 Pospuesto', categoryDone: '✅ Completado', categoryRejected: '❌ Rechazado',
                createRequest: 'Crear Solicitud', latestMedia: 'Contenido Reciente', latestMediaLoading: 'Cargando...', latestMediaEmpty: 'No hay contenido reciente', latestMediaError: 'Error al cargar',
                newEpisode: '+1 episodio', newEpisodes: '+{count} episodios', typeMovie: 'Película', typeSeries: 'Serie', typeAnime: 'Anime', typeOther: 'Otro',
                timeAgo: 'hace', timeJustNow: 'ahora mismo', timeMinutes: 'min', timeHours: 'h', timeDays: 'd',
                mediaManagement: 'Contenido', mediaManagementTitle: 'Gestión de Contenido', mediaSearch: 'Buscar...', mediaTypeAll: 'Todos', mediaTypeMovie: 'Películas', mediaTypeSeries: 'Series',
                mediaSortBy: 'Ordenar por', mediaSortTitle: 'Título', mediaSortYear: 'Año', mediaSortRating: 'Calificación', mediaSortPlays: 'Reproducciones', mediaSortSize: 'Tamaño',
                mediaSortDateAdded: 'Fecha Agregado', mediaSortPlayCount: 'Reproducciones', mediaSortWatchTime: 'Tiempo Visto', mediaLoading: 'Cargando...', mediaNoResults: 'Sin resultados', mediaError: 'Error al cargar',
                mediaScheduleDelete: 'Programar Eliminación', mediaCancelDelete: 'Cancelar Eliminación', mediaDeleteIn: 'Eliminar en', mediaLeavingIn: 'Se elimina en',
                media1Day: '1 Día', media3Days: '3 Días', media1Week: '1 Semana', media2Weeks: '2 Semanas', mediaCustom: 'Personalizado...', mediaCustomHours: 'Horas',
                mediaSchedule: 'Programar', mediaCancel: 'Cancelar', mediaNoScheduled: 'Sin eliminaciones programadas', mediaScheduledBy: 'Programado por', mediaDeletesIn: 'Se elimina en',
                mediaActions: 'Acciones', mediaChange: 'Cambiar', mediaChangeTime: 'Cambiar tiempo', mediaSoon: 'Pronto', mediaDays: 'días', mediaPlays: 'reproducciones',
                mediaMinutes: 'min', mediaGB: 'GB', mediaMB: 'MB', mediaPage: 'Página', mediaOf: 'de', mediaPrev: 'Anterior', mediaNext: 'Siguiente', mediaGo: 'Ir',
                mediaTypeScheduled: 'Programados', mediaSettings: 'Ajustes', mediaIncludeTypes: 'Incluir tipos:', mediaTypesHint: 'Selecciona los tipos a mostrar',
                requestDeleteRequest: 'Solicitar eliminar', requestDeleteMedia: 'Solicitar eliminar contenido', deletionRequests: 'Solicitudes de Eliminación', noDeletionRequests: 'Sin solicitudes de eliminación',
                deleteNow: 'Eliminar ~1h', schedule1Day: '1 Día', schedule1Week: '1 Semana', schedule1Month: '1 Mes', rejectDeletion: 'Rechazar', approveDeleteRequest: 'Aprobar',
                alreadyRequested: 'Eliminación Solicitada', deletionApproved: 'APROBADO', deletionRejected: 'RECHAZADO', deletionPending: 'PENDIENTE',
                deletionRequestSent: '¡Solicitud de eliminación enviada!', deletionRequestFailed: 'Error al enviar solicitud', deletionActionFailed: 'Error al procesar',
                deleteRequest: 'Eliminar Solicitud', deleteMedia: 'Eliminar Contenido', rejectionReasonPrompt: 'Motivo del rechazo (opcional):', rejectionReasonLabel: 'Motivo:',
                deletionLimitReached: 'Límite de solicitudes alcanzado', banUser: 'Banear', unbanUser: 'Desbanear', bannedUsers: 'Usuarios Baneados',
                ban1Day: '1 Día', ban1Week: '1 Semana', ban1Month: '1 Mes', banPermanent: 'Permanente', banExpires: 'Expira:', banPermanentLabel: 'Permanente',
                bannedBy: 'por', noBannedUsers: 'Sin usuarios baneados', youAreBanned: 'Estás baneado de esta acción', banSuccess: 'Usuario baneado', unbanSuccess: 'Usuario desbaneado',
                liveChat: 'Chat en Vivo', chatOnline: 'en línea', chatTyping: 'está escribiendo...', chatTypingMultiple: 'están escribiendo...',
                chatSend: 'Enviar', chatPlaceholder: 'Escribe un mensaje...', chatNoMessages: 'Sin mensajes. ¡Inicia la conversación!',
                chatSearchGif: 'Buscar GIFs...', chatTrending: 'Tendencias', chatPoweredBy: 'Desarrollado por Tenor',
                chatDeleted: 'Mensaje eliminado', chatYou: 'Tú', chatJustNow: 'Ahora', chatMinutesAgo: 'm', chatHoursAgo: 'h', chatYesterday: 'Ayer',
                chatModerators: 'Moderadores', chatAddMod: 'Agregar Moderador', chatRemoveMod: 'Quitar', chatBannedUsers: 'Usuarios Baneados',
                chatBan: 'Banear', chatUnban: 'Desbanear', chatMute: 'Silenciar', chatBanFromMedia: 'Banear de Media', chatMinutes: 'minutos',
                chatBanned: 'Estás baneado del chat', chatMuted: 'Estás temporalmente silenciado', chatRateLimited: '¡Más despacio! Demasiados mensajes.',
                chatConnecting: 'Conectando...', chatReconnecting: 'Reconectando...', chatDisconnected: 'Desconectado',
                chatClearAll: 'Limpiar Todo', chatClearConfirm: '¿Eliminar todos los mensajes? Esto no se puede deshacer.', chatCleared: 'Chat limpiado'
            },
            zh: {
                requestMedia: '请求媒体', manageRequests: '管理请求', requestDescription: '📬 请求您喜欢的媒体！',
                requestDescriptionText: '使用此表单请求您想观看的电影或电视剧。管理员将审核您的请求。',
                mediaTitle: '媒体标题 *', mediaTitlePlaceholder: '例如：绝命毒师、教父', type: '类型 *', selectType: '-- 选择类型 --',
                movie: '电影', tvSeries: '电视剧', anime: '动漫', documentary: '纪录片', other: '其他',
                additionalNotes: '附加说明', notesPlaceholder: '季数、年份、具体细节等', submitRequest: '提交请求',
                yourRequests: '您的请求', loadingRequests: '正在加载您的请求...', noRequests: '您还没有请求任何媒体',
                errorLoading: '加载请求时出错', notSpecified: '未指定', noDetails: '无详情', watchNow: '🎬 立即观看',
                noRequestsYet: '暂无媒体请求', newRequest: '新请求', pending: '待处理', processing: '处理中', done: '已完成', rejected: '已拒绝',
                titleRequired: '请输入媒体标题', typeRequired: '请选择媒体类型', requestSubmitted: '请求提交成功！',
                requestFailed: '提交请求失败', statusUpdated: '状态已更新', statusUpdateFailed: '更新状态失败',
                addLink: '+ 链接', enterMediaLink: '输入媒体链接：', delete: '删除', confirmDelete: '确定要删除此请求吗？',
                mediaLinkPlaceholder: '媒体链接', unknown: '未知', loading: '加载中...',
                snooze: '暂停', unsnooze: '取消暂停', snoozed: '已暂停', snoozedUntil: '暂停至', snoozeDate: '暂停至日期',
                categoryNew: '🆕 新', categoryProcessing: '🔄 处理中', categoryPending: '⏳ 待处理', categorySnoozed: '💤 已暂停', categoryDone: '✅ 已完成', categoryRejected: '❌ 已拒绝',
                createRequest: '创建请求', latestMedia: '最新媒体', latestMediaLoading: '加载中...', latestMediaEmpty: '没有最近的媒体', latestMediaError: '加载失败',
                newEpisode: '+1 集', newEpisodes: '+{count} 集', typeMovie: '电影', typeSeries: '剧集', typeAnime: '动漫', typeOther: '其他',
                timeAgo: '前', timeJustNow: '刚刚', timeMinutes: '分钟', timeHours: '小时', timeDays: '天',
                mediaManagement: '媒体', mediaManagementTitle: '媒体管理', mediaSearch: '搜索...', mediaTypeAll: '全部类型', mediaTypeMovie: '电影', mediaTypeSeries: '剧集',
                mediaSortBy: '排序', mediaSortTitle: '标题', mediaSortYear: '年份', mediaSortRating: '评分', mediaSortPlays: '播放次数', mediaSortSize: '大小',
                mediaSortDateAdded: '添加日期', mediaSortPlayCount: '播放次数', mediaSortWatchTime: '观看时长', mediaLoading: '加载中...', mediaNoResults: '未找到媒体', mediaError: '加载出错',
                mediaScheduleDelete: '计划删除', mediaCancelDelete: '取消删除', mediaDeleteIn: '删除于', mediaLeavingIn: '将于',
                media1Day: '1天', media3Days: '3天', media1Week: '1周', media2Weeks: '2周', mediaCustom: '自定义...', mediaCustomHours: '小时',
                mediaSchedule: '计划', mediaCancel: '取消', mediaNoScheduled: '没有计划删除', mediaScheduledBy: '计划者', mediaDeletesIn: '删除于',
                mediaActions: '操作', mediaChange: '更改', mediaChangeTime: '更改时间', mediaSoon: '即将', mediaDays: '天', mediaPlays: '次播放',
                mediaMinutes: '分钟', mediaGB: 'GB', mediaMB: 'MB', mediaPage: '页', mediaOf: '/', mediaPrev: '上一页', mediaNext: '下一页', mediaGo: '前往',
                mediaTypeScheduled: '已计划', mediaSettings: '设置', mediaIncludeTypes: '包含类型:', mediaTypesHint: '选择要显示的媒体类型',
                requestDeleteRequest: '请求删除', requestDeleteMedia: '请求删除媒体', deletionRequests: '删除请求', noDeletionRequests: '暂无删除请求',
                deleteNow: '立即删除', schedule1Day: '1天', schedule1Week: '1周', schedule1Month: '1个月', rejectDeletion: '拒绝', approveDeleteRequest: '批准',
                alreadyRequested: '已请求删除', deletionApproved: '已批准', deletionRejected: '已拒绝', deletionPending: '待处理',
                deletionRequestSent: '删除请求已发送！', deletionRequestFailed: '发送删除请求失败', deletionActionFailed: '处理失败',
                deleteRequest: '删除请求', deleteMedia: '删除媒体', rejectionReasonPrompt: '输入拒绝原因（可选）：', rejectionReasonLabel: '原因：',
                deletionLimitReached: '已达到删除请求上限', banUser: '封禁', unbanUser: '解封', bannedUsers: '已封禁用户',
                ban1Day: '1天', ban1Week: '1周', ban1Month: '1个月', banPermanent: '永久', banExpires: '到期：', banPermanentLabel: '永久',
                bannedBy: '由', noBannedUsers: '没有封禁用户', youAreBanned: '您已被禁止此操作', banSuccess: '用户已封禁', unbanSuccess: '用户已解封',
                liveChat: '实时聊天', chatOnline: '在线', chatTyping: '正在输入...', chatTypingMultiple: '正在输入...',
                chatSend: '发送', chatPlaceholder: '输入消息...', chatNoMessages: '暂无消息，开始聊天吧！',
                chatSearchGif: '搜索GIF...', chatTrending: '热门', chatPoweredBy: '由Tenor提供',
                chatDeleted: '消息已删除', chatYou: '你', chatJustNow: '刚刚', chatMinutesAgo: '分钟前', chatHoursAgo: '小时前', chatYesterday: '昨天',
                chatModerators: '管理员', chatAddMod: '添加管理员', chatRemoveMod: '移除', chatBannedUsers: '已封禁用户',
                chatBan: '封禁', chatUnban: '解封', chatMute: '禁言', chatBanFromMedia: '禁止访问媒体', chatMinutes: '分钟',
                chatBanned: '您已被禁止聊天', chatMuted: '您已被临时禁言', chatRateLimited: '请慢一点！消息太多了。',
                chatConnecting: '连接中...', chatReconnecting: '重新连接中...', chatDisconnected: '已断开连接',
                chatClearAll: '清除全部', chatClearConfirm: '删除所有聊天消息？此操作无法撤销。', chatCleared: '聊天已清除'
            },
            pt: {
                requestMedia: 'Solicitar Mídia', manageRequests: 'Gerenciar Solicitações', requestDescription: '📬 Solicite sua Mídia Favorita!',
                requestDescriptionText: 'Use este formulário para solicitar filmes ou séries que você gostaria de assistir.',
                mediaTitle: 'Título *', mediaTitlePlaceholder: 'ex., Breaking Bad, O Poderoso Chefão', type: 'Tipo *', selectType: '-- Selecionar Tipo --',
                movie: 'Filme', tvSeries: 'Série de TV', anime: 'Anime', documentary: 'Documentário', other: 'Outro',
                additionalNotes: 'Notas Adicionais', notesPlaceholder: 'Número da temporada, ano, detalhes específicos, etc.', submitRequest: 'Enviar Solicitação',
                yourRequests: 'Suas Solicitações', loadingRequests: 'Carregando suas solicitações...', noRequests: 'Você ainda não solicitou nenhuma mídia',
                errorLoading: 'Erro ao carregar suas solicitações', notSpecified: 'Não especificado', noDetails: 'Sem detalhes', watchNow: '🎬 Assistir Agora',
                noRequestsYet: 'Nenhuma solicitação ainda', newRequest: 'Nova Solicitação', pending: 'PENDENTE', processing: 'PROCESSANDO', done: 'CONCLUÍDO', rejected: 'REJEITADO',
                titleRequired: 'Por favor insira um título', typeRequired: 'Por favor selecione um tipo', requestSubmitted: 'Solicitação enviada com sucesso!',
                requestFailed: 'Falha ao enviar solicitação', statusUpdated: 'Status atualizado', statusUpdateFailed: 'Falha ao atualizar status',
                addLink: '+ Link', enterMediaLink: 'Insira o link:', delete: 'Excluir', confirmDelete: 'Tem certeza que deseja excluir esta solicitação?',
                mediaLinkPlaceholder: 'Link da mídia', unknown: 'Desconhecido', loading: 'Carregando...',
                snooze: 'Adiar', unsnooze: 'Reativar', snoozed: 'ADIADO', snoozedUntil: 'Adiado até', snoozeDate: 'Adiar até data',
                categoryNew: '🆕 Novo', categoryProcessing: '🔄 Processando', categoryPending: '⏳ Pendente', categorySnoozed: '💤 Adiado', categoryDone: '✅ Concluído', categoryRejected: '❌ Rejeitado',
                createRequest: 'Criar Solicitação', latestMedia: 'Mídia Recente', latestMediaLoading: 'Carregando...', latestMediaEmpty: 'Nenhuma mídia recente', latestMediaError: 'Falha ao carregar',
                newEpisode: '+1 episódio', newEpisodes: '+{count} episódios', typeMovie: 'Filme', typeSeries: 'Série', typeAnime: 'Anime', typeOther: 'Outro',
                timeAgo: 'atrás', timeJustNow: 'agora mesmo', timeMinutes: 'min', timeHours: 'h', timeDays: 'd',
                mediaManagement: 'Mídia', mediaManagementTitle: 'Gerenciamento de Mídia', mediaSearch: 'Buscar...', mediaTypeAll: 'Todos', mediaTypeMovie: 'Filmes', mediaTypeSeries: 'Séries',
                mediaSortBy: 'Ordenar por', mediaSortTitle: 'Título', mediaSortYear: 'Ano', mediaSortRating: 'Avaliação', mediaSortPlays: 'Reproduções', mediaSortSize: 'Tamanho',
                mediaSortDateAdded: 'Data Adicionado', mediaSortPlayCount: 'Reproduções', mediaSortWatchTime: 'Tempo Assistido', mediaLoading: 'Carregando...', mediaNoResults: 'Nenhuma mídia', mediaError: 'Erro ao carregar',
                mediaScheduleDelete: 'Agendar Exclusão', mediaCancelDelete: 'Cancelar Exclusão', mediaDeleteIn: 'Excluir em', mediaLeavingIn: 'Sai em',
                media1Day: '1 Dia', media3Days: '3 Dias', media1Week: '1 Semana', media2Weeks: '2 Semanas', mediaCustom: 'Personalizado...', mediaCustomHours: 'Horas',
                mediaSchedule: 'Agendar', mediaCancel: 'Cancelar', mediaNoScheduled: 'Sem exclusões agendadas', mediaScheduledBy: 'Agendado por', mediaDeletesIn: 'Exclui em',
                mediaActions: 'Ações', mediaChange: 'Alterar', mediaChangeTime: 'Alterar tempo', mediaSoon: 'Em breve', mediaDays: 'dias', mediaPlays: 'reproduções',
                mediaMinutes: 'min', mediaGB: 'GB', mediaMB: 'MB', mediaPage: 'Página', mediaOf: 'de', mediaPrev: 'Anterior', mediaNext: 'Próximo', mediaGo: 'Ir',
                mediaTypeScheduled: 'Agendados', mediaSettings: 'Configurações', mediaIncludeTypes: 'Incluir tipos:', mediaTypesHint: 'Selecione os tipos a mostrar',
                requestDeleteRequest: 'Solicitar exclusão', requestDeleteMedia: 'Solicitar excluir mídia', deletionRequests: 'Solicitações de Exclusão', noDeletionRequests: 'Sem solicitações de exclusão',
                deleteNow: 'Excluir ~1h', schedule1Day: '1 Dia', schedule1Week: '1 Semana', schedule1Month: '1 Mês', rejectDeletion: 'Rejeitar', approveDeleteRequest: 'Aprovar',
                alreadyRequested: 'Exclusão Solicitada', deletionApproved: 'APROVADO', deletionRejected: 'REJEITADO', deletionPending: 'PENDENTE',
                deletionRequestSent: 'Solicitação de exclusão enviada!', deletionRequestFailed: 'Falha ao enviar', deletionActionFailed: 'Falha ao processar',
                deleteRequest: 'Excluir Solicitação', deleteMedia: 'Excluir Mídia', rejectionReasonPrompt: 'Motivo da rejeição (opcional):', rejectionReasonLabel: 'Motivo:',
                deletionLimitReached: 'Limite de solicitações atingido', banUser: 'Banir', unbanUser: 'Desbanir', bannedUsers: 'Usuários Banidos',
                ban1Day: '1 Dia', ban1Week: '1 Semana', ban1Month: '1 Mês', banPermanent: 'Permanente', banExpires: 'Expira:', banPermanentLabel: 'Permanente',
                bannedBy: 'por', noBannedUsers: 'Sem usuários banidos', youAreBanned: 'Você está banido desta ação', banSuccess: 'Usuário banido', unbanSuccess: 'Usuário desbanido',
                liveChat: 'Chat ao Vivo', chatOnline: 'online', chatTyping: 'está digitando...', chatTypingMultiple: 'estão digitando...',
                chatSend: 'Enviar', chatPlaceholder: 'Digite uma mensagem...', chatNoMessages: 'Sem mensagens. Inicie a conversa!',
                chatSearchGif: 'Buscar GIFs...', chatTrending: 'Tendências', chatPoweredBy: 'Desenvolvido por Tenor',
                chatDeleted: 'Mensagem excluída', chatYou: 'Você', chatJustNow: 'Agora', chatMinutesAgo: 'm', chatHoursAgo: 'h', chatYesterday: 'Ontem',
                chatModerators: 'Moderadores', chatAddMod: 'Adicionar Moderador', chatRemoveMod: 'Remover', chatBannedUsers: 'Usuários Banidos',
                chatBan: 'Banir', chatUnban: 'Desbanir', chatMute: 'Silenciar', chatBanFromMedia: 'Banir de Mídia', chatMinutes: 'minutos',
                chatBanned: 'Você está banido do chat', chatMuted: 'Você está temporariamente silenciado', chatRateLimited: 'Devagar! Muitas mensagens.',
                chatConnecting: 'Conectando...', chatReconnecting: 'Reconectando...', chatDisconnected: 'Desconectado',
                chatClearAll: 'Limpar Tudo', chatClearConfirm: 'Excluir todas as mensagens? Isso não pode ser desfeito.', chatCleared: 'Chat limpo'
            },
            ru: {
                requestMedia: 'Запросить Медиа', manageRequests: 'Управление Запросами', requestDescription: '📬 Запросите Любимый Контент!',
                requestDescriptionText: 'Используйте эту форму для запроса фильмов или сериалов, которые вы хотели бы посмотреть.',
                mediaTitle: 'Название *', mediaTitlePlaceholder: 'напр., Во все тяжкие, Крёстный отец', type: 'Тип *', selectType: '-- Выберите Тип --',
                movie: 'Фильм', tvSeries: 'Сериал', anime: 'Аниме', documentary: 'Документальный', other: 'Другое',
                additionalNotes: 'Дополнительные Заметки', notesPlaceholder: 'Номер сезона, год, конкретные детали и т.д.', submitRequest: 'Отправить Запрос',
                yourRequests: 'Ваши Запросы', loadingRequests: 'Загрузка ваших запросов...', noRequests: 'Вы ещё не запрашивали медиа',
                errorLoading: 'Ошибка загрузки запросов', notSpecified: 'Не указано', noDetails: 'Нет деталей', watchNow: '🎬 Смотреть',
                noRequestsYet: 'Запросов пока нет', newRequest: 'Новый Запрос', pending: 'ОЖИДАНИЕ', processing: 'В ОБРАБОТКЕ', done: 'ГОТОВО', rejected: 'ОТКЛОНЕНО',
                titleRequired: 'Пожалуйста, введите название', typeRequired: 'Пожалуйста, выберите тип', requestSubmitted: 'Запрос успешно отправлен!',
                requestFailed: 'Не удалось отправить запрос', statusUpdated: 'Статус обновлён', statusUpdateFailed: 'Не удалось обновить статус',
                addLink: '+ Ссылка', enterMediaLink: 'Введите ссылку:', delete: 'Удалить', confirmDelete: 'Вы уверены, что хотите удалить этот запрос?',
                mediaLinkPlaceholder: 'Ссылка на медиа', unknown: 'Неизвестно', loading: 'Загрузка...',
                snooze: 'Отложить', unsnooze: 'Возобновить', snoozed: 'ОТЛОЖЕНО', snoozedUntil: 'Отложено до', snoozeDate: 'Отложить до даты',
                categoryNew: '🆕 Новые', categoryProcessing: '🔄 В обработке', categoryPending: '⏳ Ожидание', categorySnoozed: '💤 Отложено', categoryDone: '✅ Готово', categoryRejected: '❌ Отклонено',
                createRequest: 'Создать Запрос', latestMedia: 'Последние Добавления', latestMediaLoading: 'Загрузка...', latestMediaEmpty: 'Нет недавних медиа', latestMediaError: 'Ошибка загрузки',
                newEpisode: '+1 эпизод', newEpisodes: '+{count} эпизодов', typeMovie: 'Фильм', typeSeries: 'Сериал', typeAnime: 'Аниме', typeOther: 'Другое',
                timeAgo: 'назад', timeJustNow: 'только что', timeMinutes: 'мин', timeHours: 'ч', timeDays: 'д',
                mediaManagement: 'Медиа', mediaManagementTitle: 'Управление Медиа', mediaSearch: 'Поиск...', mediaTypeAll: 'Все Типы', mediaTypeMovie: 'Фильмы', mediaTypeSeries: 'Сериалы',
                mediaSortBy: 'Сортировать', mediaSortTitle: 'Название', mediaSortYear: 'Год', mediaSortRating: 'Рейтинг', mediaSortPlays: 'Просмотры', mediaSortSize: 'Размер',
                mediaSortDateAdded: 'Дата Добавления', mediaSortPlayCount: 'Просмотры', mediaSortWatchTime: 'Время Просмотра', mediaLoading: 'Загрузка...', mediaNoResults: 'Медиа не найдено', mediaError: 'Ошибка загрузки',
                mediaScheduleDelete: 'Запланировать Удаление', mediaCancelDelete: 'Отменить Удаление', mediaDeleteIn: 'Удалить через', mediaLeavingIn: 'Удаление через',
                media1Day: '1 День', media3Days: '3 Дня', media1Week: '1 Неделю', media2Weeks: '2 Недели', mediaCustom: 'Другое...', mediaCustomHours: 'Часов',
                mediaSchedule: 'Запланировать', mediaCancel: 'Отмена', mediaNoScheduled: 'Нет запланированных удалений', mediaScheduledBy: 'Запланировал', mediaDeletesIn: 'Удаление через',
                mediaActions: 'Действия', mediaChange: 'Изменить', mediaChangeTime: 'Изменить время', mediaSoon: 'Скоро', mediaDays: 'дней', mediaPlays: 'просмотров',
                mediaMinutes: 'мин', mediaGB: 'ГБ', mediaMB: 'МБ', mediaPage: 'Страница', mediaOf: 'из', mediaPrev: 'Назад', mediaNext: 'Вперёд', mediaGo: 'Перейти',
                mediaTypeScheduled: 'Запланировано', mediaSettings: 'Настройки', mediaIncludeTypes: 'Включить типы:', mediaTypesHint: 'Выберите типы для отображения',
                requestDeleteRequest: 'Запросить удаление', requestDeleteMedia: 'Запросить удаление медиа', deletionRequests: 'Запросы на Удаление', noDeletionRequests: 'Нет запросов на удаление',
                deleteNow: 'Удалить ~1ч', schedule1Day: '1 День', schedule1Week: '1 Неделя', schedule1Month: '1 Месяц', rejectDeletion: 'Отклонить', approveDeleteRequest: 'Одобрить',
                alreadyRequested: 'Удаление Запрошено', deletionApproved: 'ОДОБРЕНО', deletionRejected: 'ОТКЛОНЕНО', deletionPending: 'ОЖИДАНИЕ',
                deletionRequestSent: 'Запрос на удаление отправлен!', deletionRequestFailed: 'Не удалось отправить', deletionActionFailed: 'Ошибка обработки',
                deleteRequest: 'Удалить Запрос', deleteMedia: 'Удалить Медиа', rejectionReasonPrompt: 'Причина отклонения (необязательно):', rejectionReasonLabel: 'Причина:',
                deletionLimitReached: 'Достигнут лимит запросов', banUser: 'Заблокировать', unbanUser: 'Разблокировать', bannedUsers: 'Заблокированные',
                ban1Day: '1 День', ban1Week: '1 Неделя', ban1Month: '1 Месяц', banPermanent: 'Навсегда', banExpires: 'Истекает:', banPermanentLabel: 'Навсегда',
                bannedBy: 'заблокировал', noBannedUsers: 'Нет заблокированных', youAreBanned: 'Вы заблокированы', banSuccess: 'Пользователь заблокирован', unbanSuccess: 'Пользователь разблокирован',
                liveChat: 'Чат', chatOnline: 'онлайн', chatTyping: 'печатает...', chatTypingMultiple: 'печатают...',
                chatSend: 'Отправить', chatPlaceholder: 'Введите сообщение...', chatNoMessages: 'Нет сообщений. Начните разговор!',
                chatSearchGif: 'Поиск GIF...', chatTrending: 'Популярные', chatPoweredBy: 'При поддержке Tenor',
                chatDeleted: 'Сообщение удалено', chatYou: 'Вы', chatJustNow: 'Только что', chatMinutesAgo: 'м', chatHoursAgo: 'ч', chatYesterday: 'Вчера',
                chatModerators: 'Модераторы', chatAddMod: 'Добавить модератора', chatRemoveMod: 'Удалить', chatBannedUsers: 'Заблокированные',
                chatBan: 'Заблокировать', chatUnban: 'Разблокировать', chatMute: 'Заглушить', chatBanFromMedia: 'Заблокировать медиа', chatMinutes: 'минут',
                chatBanned: 'Вы заблокированы в чате', chatMuted: 'Вы временно заглушены', chatRateLimited: 'Помедленнее! Слишком много сообщений.',
                chatConnecting: 'Подключение...', chatReconnecting: 'Переподключение...', chatDisconnected: 'Отключено',
                chatClearAll: 'Очистить всё', chatClearConfirm: 'Удалить все сообщения? Это действие нельзя отменить.', chatCleared: 'Чат очищен'
            },
            ja: {
                requestMedia: 'メディアをリクエスト', manageRequests: 'リクエスト管理', requestDescription: '📬 お気に入りのメディアをリクエスト！',
                requestDescriptionText: 'このフォームを使用して、視聴したい映画やテレビシリーズをリクエストしてください。',
                mediaTitle: 'メディアタイトル *', mediaTitlePlaceholder: '例：ブレイキング・バッド、ゴッドファーザー', type: 'タイプ *', selectType: '-- タイプを選択 --',
                movie: '映画', tvSeries: 'テレビシリーズ', anime: 'アニメ', documentary: 'ドキュメンタリー', other: 'その他',
                additionalNotes: '追加メモ', notesPlaceholder: 'シーズン番号、年、詳細など', submitRequest: 'リクエストを送信',
                yourRequests: 'あなたのリクエスト', loadingRequests: 'リクエストを読み込み中...', noRequests: 'まだリクエストがありません',
                errorLoading: 'リクエストの読み込みエラー', notSpecified: '未指定', noDetails: '詳細なし', watchNow: '🎬 今すぐ視聴',
                noRequestsYet: 'リクエストはまだありません', newRequest: '新しいリクエスト', pending: '保留中', processing: '処理中', done: '完了', rejected: '却下',
                titleRequired: 'タイトルを入力してください', typeRequired: 'タイプを選択してください', requestSubmitted: 'リクエストが送信されました！',
                requestFailed: 'リクエストの送信に失敗', statusUpdated: 'ステータス更新済み', statusUpdateFailed: 'ステータス更新に失敗',
                addLink: '+ リンク', enterMediaLink: 'リンクを入力:', delete: '削除', confirmDelete: 'このリクエストを削除しますか？',
                mediaLinkPlaceholder: 'メディアリンク', unknown: '不明', loading: '読み込み中...',
                snooze: 'スヌーズ', unsnooze: '再開', snoozed: 'スヌーズ中', snoozedUntil: 'スヌーズ期限', snoozeDate: 'スヌーズ日付',
                categoryNew: '🆕 新規', categoryProcessing: '🔄 処理中', categoryPending: '⏳ 保留中', categorySnoozed: '💤 スヌーズ', categoryDone: '✅ 完了', categoryRejected: '❌ 却下',
                createRequest: 'リクエスト作成', latestMedia: '最新メディア', latestMediaLoading: '読み込み中...', latestMediaEmpty: '最近のメディアなし', latestMediaError: '読み込み失敗',
                newEpisode: '+1 エピソード', newEpisodes: '+{count} エピソード', typeMovie: '映画', typeSeries: 'シリーズ', typeAnime: 'アニメ', typeOther: 'その他',
                timeAgo: '前', timeJustNow: 'たった今', timeMinutes: '分', timeHours: '時間', timeDays: '日',
                mediaManagement: 'メディア', mediaManagementTitle: 'メディア管理', mediaSearch: '検索...', mediaTypeAll: 'すべて', mediaTypeMovie: '映画', mediaTypeSeries: 'シリーズ',
                mediaSortBy: '並べ替え', mediaSortTitle: 'タイトル', mediaSortYear: '年', mediaSortRating: '評価', mediaSortPlays: '再生回数', mediaSortSize: 'サイズ',
                mediaSortDateAdded: '追加日', mediaSortPlayCount: '再生回数', mediaSortWatchTime: '視聴時間', mediaLoading: '読み込み中...', mediaNoResults: 'メディアなし', mediaError: '読み込みエラー',
                mediaScheduleDelete: '削除予約', mediaCancelDelete: '削除キャンセル', mediaDeleteIn: '削除まで', mediaLeavingIn: '削除予定',
                media1Day: '1日', media3Days: '3日', media1Week: '1週間', media2Weeks: '2週間', mediaCustom: 'カスタム...', mediaCustomHours: '時間',
                mediaSchedule: '予約', mediaCancel: 'キャンセル', mediaNoScheduled: '予約済み削除なし', mediaScheduledBy: '予約者', mediaDeletesIn: '削除まで',
                mediaActions: 'アクション', mediaChange: '変更', mediaChangeTime: '時間変更', mediaSoon: 'もうすぐ', mediaDays: '日', mediaPlays: '回再生',
                mediaMinutes: '分', mediaGB: 'GB', mediaMB: 'MB', mediaPage: 'ページ', mediaOf: '/', mediaPrev: '前へ', mediaNext: '次へ', mediaGo: '移動',
                mediaTypeScheduled: '予約済み', mediaSettings: '設定', mediaIncludeTypes: '含めるタイプ:', mediaTypesHint: '表示するタイプを選択',
                requestDeleteRequest: '削除リクエスト', requestDeleteMedia: 'メディア削除リクエスト', deletionRequests: '削除リクエスト', noDeletionRequests: '削除リクエストなし',
                deleteNow: '即削除', schedule1Day: '1日', schedule1Week: '1週間', schedule1Month: '1ヶ月', rejectDeletion: '却下', approveDeleteRequest: '承認',
                alreadyRequested: '削除リクエスト済み', deletionApproved: '承認済み', deletionRejected: '却下済み', deletionPending: '保留中',
                deletionRequestSent: '削除リクエスト送信済み！', deletionRequestFailed: '送信失敗', deletionActionFailed: '処理失敗',
                deleteRequest: 'リクエスト削除', deleteMedia: 'メディア削除', rejectionReasonPrompt: '却下理由（任意）:', rejectionReasonLabel: '理由:',
                deletionLimitReached: 'リクエスト上限に達しました', banUser: 'BAN', unbanUser: 'BAN解除', bannedUsers: 'BANユーザー',
                ban1Day: '1日', ban1Week: '1週間', ban1Month: '1ヶ月', banPermanent: '永久', banExpires: '期限:', banPermanentLabel: '永久',
                bannedBy: 'by', noBannedUsers: 'BANユーザーなし', youAreBanned: 'この操作は禁止されています', banSuccess: 'ユーザーをBANしました', unbanSuccess: 'BANを解除しました',
                liveChat: 'ライブチャット', chatOnline: 'オンライン', chatTyping: '入力中...', chatTypingMultiple: '入力中...',
                chatSend: '送信', chatPlaceholder: 'メッセージを入力...', chatNoMessages: 'メッセージがありません。会話を始めましょう！',
                chatSearchGif: 'GIF検索...', chatTrending: 'トレンド', chatPoweredBy: 'Tenor提供',
                chatDeleted: 'メッセージ削除済み', chatYou: 'あなた', chatJustNow: 'たった今', chatMinutesAgo: '分前', chatHoursAgo: '時間前', chatYesterday: '昨日',
                chatModerators: 'モデレーター', chatAddMod: 'モデレーター追加', chatRemoveMod: '削除', chatBannedUsers: 'BANユーザー',
                chatBan: 'BAN', chatUnban: 'BAN解除', chatMute: 'ミュート', chatBanFromMedia: 'メディアBAN', chatMinutes: '分',
                chatBanned: 'チャットからBANされています', chatMuted: '一時的にミュートされています', chatRateLimited: '少し待ってください！メッセージが多すぎます。',
                chatConnecting: '接続中...', chatReconnecting: '再接続中...', chatDisconnected: '切断されました',
                chatClearAll: '全削除', chatClearConfirm: 'すべてのメッセージを削除しますか？この操作は取り消せません。', chatCleared: 'チャットを削除しました'
            },
            de: {
                requestMedia: 'Medien Anfordern', manageRequests: 'Anfragen Verwalten', requestDescription: '📬 Fordere deine Lieblingsmedien an!',
                requestDescriptionText: 'Verwende dieses Formular, um Filme oder Serien anzufordern, die du sehen möchtest.',
                mediaTitle: 'Medientitel *', mediaTitlePlaceholder: 'z.B., Breaking Bad, Der Pate', type: 'Typ *', selectType: '-- Typ auswählen --',
                movie: 'Film', tvSeries: 'TV-Serie', anime: 'Anime', documentary: 'Dokumentation', other: 'Sonstiges',
                additionalNotes: 'Zusätzliche Notizen', notesPlaceholder: 'Staffelnummer, Jahr, spezifische Details, etc.', submitRequest: 'Anfrage Senden',
                yourRequests: 'Deine Anfragen', loadingRequests: 'Lade deine Anfragen...', noRequests: 'Du hast noch keine Medien angefordert',
                errorLoading: 'Fehler beim Laden', notSpecified: 'Nicht angegeben', noDetails: 'Keine Details', watchNow: '🎬 Jetzt Ansehen',
                noRequestsYet: 'Noch keine Anfragen', newRequest: 'Neue Anfrage', pending: 'AUSSTEHEND', processing: 'IN BEARBEITUNG', done: 'ERLEDIGT', rejected: 'ABGELEHNT',
                titleRequired: 'Bitte Titel eingeben', typeRequired: 'Bitte Typ auswählen', requestSubmitted: 'Anfrage erfolgreich gesendet!',
                requestFailed: 'Anfrage fehlgeschlagen', statusUpdated: 'Status aktualisiert', statusUpdateFailed: 'Status-Update fehlgeschlagen',
                addLink: '+ Link', enterMediaLink: 'Link eingeben:', delete: 'Löschen', confirmDelete: 'Diese Anfrage wirklich löschen?',
                mediaLinkPlaceholder: 'Medien-Link', unknown: 'Unbekannt', loading: 'Laden...',
                snooze: 'Zurückstellen', unsnooze: 'Reaktivieren', snoozed: 'ZURÜCKGESTELLT', snoozedUntil: 'Zurückgestellt bis', snoozeDate: 'Zurückstellen bis',
                categoryNew: '🆕 Neu', categoryProcessing: '🔄 In Bearbeitung', categoryPending: '⏳ Ausstehend', categorySnoozed: '💤 Zurückgestellt', categoryDone: '✅ Erledigt', categoryRejected: '❌ Abgelehnt',
                createRequest: 'Anfrage Erstellen', latestMedia: 'Neueste Medien', latestMediaLoading: 'Laden...', latestMediaEmpty: 'Keine neuen Medien', latestMediaError: 'Laden fehlgeschlagen',
                newEpisode: '+1 Episode', newEpisodes: '+{count} Episoden', typeMovie: 'Film', typeSeries: 'Serie', typeAnime: 'Anime', typeOther: 'Sonstiges',
                timeAgo: 'vor', timeJustNow: 'gerade eben', timeMinutes: 'Min', timeHours: 'Std', timeDays: 'T',
                mediaManagement: 'Medien', mediaManagementTitle: 'Medienverwaltung', mediaSearch: 'Suchen...', mediaTypeAll: 'Alle Typen', mediaTypeMovie: 'Filme', mediaTypeSeries: 'Serien',
                mediaSortBy: 'Sortieren nach', mediaSortTitle: 'Titel', mediaSortYear: 'Jahr', mediaSortRating: 'Bewertung', mediaSortPlays: 'Wiedergaben', mediaSortSize: 'Größe',
                mediaSortDateAdded: 'Hinzugefügt', mediaSortPlayCount: 'Wiedergaben', mediaSortWatchTime: 'Wiedergabezeit', mediaLoading: 'Laden...', mediaNoResults: 'Keine Medien', mediaError: 'Ladefehler',
                mediaScheduleDelete: 'Löschung Planen', mediaCancelDelete: 'Löschung Abbrechen', mediaDeleteIn: 'Löschen in', mediaLeavingIn: 'Wird gelöscht in',
                media1Day: '1 Tag', media3Days: '3 Tage', media1Week: '1 Woche', media2Weeks: '2 Wochen', mediaCustom: 'Benutzerdefiniert...', mediaCustomHours: 'Stunden',
                mediaSchedule: 'Planen', mediaCancel: 'Abbrechen', mediaNoScheduled: 'Keine geplanten Löschungen', mediaScheduledBy: 'Geplant von', mediaDeletesIn: 'Löschung in',
                mediaActions: 'Aktionen', mediaChange: 'Ändern', mediaChangeTime: 'Zeit ändern', mediaSoon: 'Bald', mediaDays: 'Tage', mediaPlays: 'Wiedergaben',
                mediaMinutes: 'Min', mediaGB: 'GB', mediaMB: 'MB', mediaPage: 'Seite', mediaOf: 'von', mediaPrev: 'Zurück', mediaNext: 'Weiter', mediaGo: 'Los',
                mediaTypeScheduled: 'Geplant', mediaSettings: 'Einstellungen', mediaIncludeTypes: 'Medientypen:', mediaTypesHint: 'Anzuzeigende Typen auswählen',
                requestDeleteRequest: 'Löschung anfordern', requestDeleteMedia: 'Medienlöschung anfordern', deletionRequests: 'Löschanfragen', noDeletionRequests: 'Keine Löschanfragen',
                deleteNow: 'Jetzt löschen', schedule1Day: '1 Tag', schedule1Week: '1 Woche', schedule1Month: '1 Monat', rejectDeletion: 'Ablehnen', approveDeleteRequest: 'Genehmigen',
                alreadyRequested: 'Löschung Angefordert', deletionApproved: 'GENEHMIGT', deletionRejected: 'ABGELEHNT', deletionPending: 'AUSSTEHEND',
                deletionRequestSent: 'Löschanfrage gesendet!', deletionRequestFailed: 'Senden fehlgeschlagen', deletionActionFailed: 'Verarbeitung fehlgeschlagen',
                deleteRequest: 'Anfrage Löschen', deleteMedia: 'Medien Löschen', rejectionReasonPrompt: 'Ablehnungsgrund (optional):', rejectionReasonLabel: 'Grund:',
                deletionLimitReached: 'Anfragenlimit erreicht', banUser: 'Sperren', unbanUser: 'Entsperren', bannedUsers: 'Gesperrte Benutzer',
                ban1Day: '1 Tag', ban1Week: '1 Woche', ban1Month: '1 Monat', banPermanent: 'Permanent', banExpires: 'Läuft ab:', banPermanentLabel: 'Permanent',
                bannedBy: 'von', noBannedUsers: 'Keine gesperrten Benutzer', youAreBanned: 'Du bist für diese Aktion gesperrt', banSuccess: 'Benutzer gesperrt', unbanSuccess: 'Benutzer entsperrt',
                liveChat: 'Live-Chat', chatOnline: 'online', chatTyping: 'tippt...', chatTypingMultiple: 'tippen...',
                chatSend: 'Senden', chatPlaceholder: 'Nachricht eingeben...', chatNoMessages: 'Keine Nachrichten. Starte die Unterhaltung!',
                chatSearchGif: 'GIFs suchen...', chatTrending: 'Trends', chatPoweredBy: 'Powered by Tenor',
                chatDeleted: 'Nachricht gelöscht', chatYou: 'Du', chatJustNow: 'Gerade eben', chatMinutesAgo: 'Min', chatHoursAgo: 'Std', chatYesterday: 'Gestern',
                chatModerators: 'Moderatoren', chatAddMod: 'Moderator hinzufügen', chatRemoveMod: 'Entfernen', chatBannedUsers: 'Gesperrte Benutzer',
                chatBan: 'Sperren', chatUnban: 'Entsperren', chatMute: 'Stummschalten', chatBanFromMedia: 'Von Medien sperren', chatMinutes: 'Minuten',
                chatBanned: 'Du bist vom Chat gesperrt', chatMuted: 'Du bist vorübergehend stummgeschaltet', chatRateLimited: 'Langsamer! Zu viele Nachrichten.',
                chatConnecting: 'Verbinden...', chatReconnecting: 'Neu verbinden...', chatDisconnected: 'Getrennt',
                chatClearAll: 'Alle löschen', chatClearConfirm: 'Alle Nachrichten löschen? Dies kann nicht rückgängig gemacht werden.', chatCleared: 'Chat gelöscht'
            },
            fr: {
                requestMedia: 'Demander un Média', manageRequests: 'Gérer les Demandes', requestDescription: '📬 Demandez vos Médias Préférés!',
                requestDescriptionText: 'Utilisez ce formulaire pour demander des films ou séries que vous aimeriez regarder.',
                mediaTitle: 'Titre *', mediaTitlePlaceholder: 'ex., Breaking Bad, Le Parrain', type: 'Type *', selectType: '-- Sélectionner Type --',
                movie: 'Film', tvSeries: 'Série TV', anime: 'Anime', documentary: 'Documentaire', other: 'Autre',
                additionalNotes: 'Notes Supplémentaires', notesPlaceholder: 'Numéro de saison, année, détails spécifiques, etc.', submitRequest: 'Envoyer la Demande',
                yourRequests: 'Vos Demandes', loadingRequests: 'Chargement de vos demandes...', noRequests: 'Vous n\'avez pas encore demandé de média',
                errorLoading: 'Erreur de chargement', notSpecified: 'Non spécifié', noDetails: 'Pas de détails', watchNow: '🎬 Regarder',
                noRequestsYet: 'Pas encore de demandes', newRequest: 'Nouvelle Demande', pending: 'EN ATTENTE', processing: 'EN COURS', done: 'TERMINÉ', rejected: 'REFUSÉ',
                titleRequired: 'Veuillez entrer un titre', typeRequired: 'Veuillez sélectionner un type', requestSubmitted: 'Demande envoyée avec succès!',
                requestFailed: 'Échec de l\'envoi', statusUpdated: 'Statut mis à jour', statusUpdateFailed: 'Échec de mise à jour',
                addLink: '+ Lien', enterMediaLink: 'Entrer le lien:', delete: 'Supprimer', confirmDelete: 'Êtes-vous sûr de vouloir supprimer cette demande?',
                mediaLinkPlaceholder: 'Lien du média', unknown: 'Inconnu', loading: 'Chargement...',
                snooze: 'Reporter', unsnooze: 'Réactiver', snoozed: 'REPORTÉ', snoozedUntil: 'Reporté jusqu\'à', snoozeDate: 'Reporter jusqu\'à',
                categoryNew: '🆕 Nouveau', categoryProcessing: '🔄 En cours', categoryPending: '⏳ En attente', categorySnoozed: '💤 Reporté', categoryDone: '✅ Terminé', categoryRejected: '❌ Refusé',
                createRequest: 'Créer Demande', latestMedia: 'Derniers Médias', latestMediaLoading: 'Chargement...', latestMediaEmpty: 'Pas de médias récents', latestMediaError: 'Échec du chargement',
                newEpisode: '+1 épisode', newEpisodes: '+{count} épisodes', typeMovie: 'Film', typeSeries: 'Série', typeAnime: 'Anime', typeOther: 'Autre',
                timeAgo: 'il y a', timeJustNow: 'à l\'instant', timeMinutes: 'min', timeHours: 'h', timeDays: 'j',
                mediaManagement: 'Médias', mediaManagementTitle: 'Gestion des Médias', mediaSearch: 'Rechercher...', mediaTypeAll: 'Tous Types', mediaTypeMovie: 'Films', mediaTypeSeries: 'Séries',
                mediaSortBy: 'Trier par', mediaSortTitle: 'Titre', mediaSortYear: 'Année', mediaSortRating: 'Note', mediaSortPlays: 'Lectures', mediaSortSize: 'Taille',
                mediaSortDateAdded: 'Date Ajout', mediaSortPlayCount: 'Lectures', mediaSortWatchTime: 'Temps Regardé', mediaLoading: 'Chargement...', mediaNoResults: 'Aucun média', mediaError: 'Erreur de chargement',
                mediaScheduleDelete: 'Planifier Suppression', mediaCancelDelete: 'Annuler Suppression', mediaDeleteIn: 'Supprimer dans', mediaLeavingIn: 'Part dans',
                media1Day: '1 Jour', media3Days: '3 Jours', media1Week: '1 Semaine', media2Weeks: '2 Semaines', mediaCustom: 'Personnalisé...', mediaCustomHours: 'Heures',
                mediaSchedule: 'Planifier', mediaCancel: 'Annuler', mediaNoScheduled: 'Aucune suppression planifiée', mediaScheduledBy: 'Planifié par', mediaDeletesIn: 'Suppression dans',
                mediaActions: 'Actions', mediaChange: 'Modifier', mediaChangeTime: 'Modifier temps', mediaSoon: 'Bientôt', mediaDays: 'jours', mediaPlays: 'lectures',
                mediaMinutes: 'min', mediaGB: 'Go', mediaMB: 'Mo', mediaPage: 'Page', mediaOf: 'sur', mediaPrev: 'Précédent', mediaNext: 'Suivant', mediaGo: 'Aller',
                mediaTypeScheduled: 'Planifiés', mediaSettings: 'Paramètres', mediaIncludeTypes: 'Inclure types:', mediaTypesHint: 'Sélectionner les types à afficher',
                requestDeleteRequest: 'Demander suppression', requestDeleteMedia: 'Demander suppression média', deletionRequests: 'Demandes de Suppression', noDeletionRequests: 'Pas de demandes de suppression',
                deleteNow: 'Supprimer ~1h', schedule1Day: '1 Jour', schedule1Week: '1 Semaine', schedule1Month: '1 Mois', rejectDeletion: 'Refuser', approveDeleteRequest: 'Approuver',
                alreadyRequested: 'Suppression Demandée', deletionApproved: 'APPROUVÉ', deletionRejected: 'REFUSÉ', deletionPending: 'EN ATTENTE',
                deletionRequestSent: 'Demande de suppression envoyée!', deletionRequestFailed: 'Échec de l\'envoi', deletionActionFailed: 'Échec du traitement',
                deleteRequest: 'Supprimer Demande', deleteMedia: 'Supprimer Média', rejectionReasonPrompt: 'Raison du refus (optionnel):', rejectionReasonLabel: 'Raison:',
                deletionLimitReached: 'Limite de demandes atteinte', banUser: 'Bannir', unbanUser: 'Débannir', bannedUsers: 'Utilisateurs Bannis',
                ban1Day: '1 Jour', ban1Week: '1 Semaine', ban1Month: '1 Mois', banPermanent: 'Permanent', banExpires: 'Expire:', banPermanentLabel: 'Permanent',
                bannedBy: 'par', noBannedUsers: 'Pas d\'utilisateurs bannis', youAreBanned: 'Vous êtes banni de cette action', banSuccess: 'Utilisateur banni', unbanSuccess: 'Utilisateur débanni',
                liveChat: 'Chat en Direct', chatOnline: 'en ligne', chatTyping: 'écrit...', chatTypingMultiple: 'écrivent...',
                chatSend: 'Envoyer', chatPlaceholder: 'Tapez un message...', chatNoMessages: 'Pas de messages. Lancez la conversation !',
                chatSearchGif: 'Rechercher GIFs...', chatTrending: 'Tendances', chatPoweredBy: 'Propulsé par Tenor',
                chatDeleted: 'Message supprimé', chatYou: 'Vous', chatJustNow: 'À l\'instant', chatMinutesAgo: 'min', chatHoursAgo: 'h', chatYesterday: 'Hier',
                chatModerators: 'Modérateurs', chatAddMod: 'Ajouter Modérateur', chatRemoveMod: 'Retirer', chatBannedUsers: 'Utilisateurs Bannis',
                chatBan: 'Bannir', chatUnban: 'Débannir', chatMute: 'Rendre muet', chatBanFromMedia: 'Bannir des Médias', chatMinutes: 'minutes',
                chatBanned: 'Vous êtes banni du chat', chatMuted: 'Vous êtes temporairement muet', chatRateLimited: 'Doucement ! Trop de messages.',
                chatConnecting: 'Connexion...', chatReconnecting: 'Reconnexion...', chatDisconnected: 'Déconnecté',
                chatClearAll: 'Tout effacer', chatClearConfirm: 'Supprimer tous les messages ? Cette action est irréversible.', chatCleared: 'Chat effacé'
            },
            ko: {
                requestMedia: '미디어 요청', manageRequests: '요청 관리', requestDescription: '📬 좋아하는 미디어를 요청하세요!',
                requestDescriptionText: '이 양식을 사용하여 시청하고 싶은 영화나 TV 시리즈를 요청하세요.',
                mediaTitle: '미디어 제목 *', mediaTitlePlaceholder: '예: 브레이킹 배드, 대부', type: '유형 *', selectType: '-- 유형 선택 --',
                movie: '영화', tvSeries: 'TV 시리즈', anime: '애니메이션', documentary: '다큐멘터리', other: '기타',
                additionalNotes: '추가 메모', notesPlaceholder: '시즌 번호, 연도, 세부 정보 등', submitRequest: '요청 제출',
                yourRequests: '내 요청', loadingRequests: '요청 로딩 중...', noRequests: '아직 요청한 미디어가 없습니다',
                errorLoading: '요청 로딩 오류', notSpecified: '미지정', noDetails: '세부 정보 없음', watchNow: '🎬 지금 보기',
                noRequestsYet: '아직 요청이 없습니다', newRequest: '새 요청', pending: '대기 중', processing: '처리 중', done: '완료', rejected: '거부됨',
                titleRequired: '제목을 입력하세요', typeRequired: '유형을 선택하세요', requestSubmitted: '요청이 제출되었습니다!',
                requestFailed: '요청 제출 실패', statusUpdated: '상태 업데이트됨', statusUpdateFailed: '상태 업데이트 실패',
                addLink: '+ 링크', enterMediaLink: '링크 입력:', delete: '삭제', confirmDelete: '이 요청을 삭제하시겠습니까?',
                mediaLinkPlaceholder: '미디어 링크', unknown: '알 수 없음', loading: '로딩 중...',
                snooze: '일시 중지', unsnooze: '재개', snoozed: '일시 중지됨', snoozedUntil: '일시 중지 기한', snoozeDate: '일시 중지 날짜',
                categoryNew: '🆕 신규', categoryProcessing: '🔄 처리 중', categoryPending: '⏳ 대기 중', categorySnoozed: '💤 일시 중지', categoryDone: '✅ 완료', categoryRejected: '❌ 거부됨',
                createRequest: '요청 생성', latestMedia: '최신 미디어', latestMediaLoading: '로딩 중...', latestMediaEmpty: '최근 미디어 없음', latestMediaError: '로딩 실패',
                newEpisode: '+1 에피소드', newEpisodes: '+{count} 에피소드', typeMovie: '영화', typeSeries: '시리즈', typeAnime: '애니메이션', typeOther: '기타',
                timeAgo: '전', timeJustNow: '방금', timeMinutes: '분', timeHours: '시간', timeDays: '일',
                mediaManagement: '미디어', mediaManagementTitle: '미디어 관리', mediaSearch: '검색...', mediaTypeAll: '모든 유형', mediaTypeMovie: '영화', mediaTypeSeries: '시리즈',
                mediaSortBy: '정렬', mediaSortTitle: '제목', mediaSortYear: '연도', mediaSortRating: '평점', mediaSortPlays: '재생 수', mediaSortSize: '크기',
                mediaSortDateAdded: '추가 날짜', mediaSortPlayCount: '재생 수', mediaSortWatchTime: '시청 시간', mediaLoading: '로딩 중...', mediaNoResults: '미디어 없음', mediaError: '로딩 오류',
                mediaScheduleDelete: '삭제 예약', mediaCancelDelete: '삭제 취소', mediaDeleteIn: '삭제 예정', mediaLeavingIn: '삭제까지',
                media1Day: '1일', media3Days: '3일', media1Week: '1주', media2Weeks: '2주', mediaCustom: '사용자 지정...', mediaCustomHours: '시간',
                mediaSchedule: '예약', mediaCancel: '취소', mediaNoScheduled: '예약된 삭제 없음', mediaScheduledBy: '예약자', mediaDeletesIn: '삭제까지',
                mediaActions: '작업', mediaChange: '변경', mediaChangeTime: '시간 변경', mediaSoon: '곧', mediaDays: '일', mediaPlays: '재생',
                mediaMinutes: '분', mediaGB: 'GB', mediaMB: 'MB', mediaPage: '페이지', mediaOf: '/', mediaPrev: '이전', mediaNext: '다음', mediaGo: '이동',
                mediaTypeScheduled: '예약됨', mediaSettings: '설정', mediaIncludeTypes: '포함 유형:', mediaTypesHint: '표시할 유형 선택',
                requestDeleteRequest: '삭제 요청', requestDeleteMedia: '미디어 삭제 요청', deletionRequests: '삭제 요청', noDeletionRequests: '삭제 요청 없음',
                deleteNow: '즉시 삭제', schedule1Day: '1일', schedule1Week: '1주', schedule1Month: '1개월', rejectDeletion: '거부', approveDeleteRequest: '승인',
                alreadyRequested: '삭제 요청됨', deletionApproved: '승인됨', deletionRejected: '거부됨', deletionPending: '대기 중',
                deletionRequestSent: '삭제 요청 전송됨!', deletionRequestFailed: '전송 실패', deletionActionFailed: '처리 실패',
                deleteRequest: '요청 삭제', deleteMedia: '미디어 삭제', rejectionReasonPrompt: '거부 사유 (선택):', rejectionReasonLabel: '사유:',
                deletionLimitReached: '요청 한도 도달', banUser: '차단', unbanUser: '차단 해제', bannedUsers: '차단된 사용자',
                ban1Day: '1일', ban1Week: '1주', ban1Month: '1개월', banPermanent: '영구', banExpires: '만료:', banPermanentLabel: '영구',
                bannedBy: '차단자', noBannedUsers: '차단된 사용자 없음', youAreBanned: '이 작업이 차단되었습니다', banSuccess: '사용자 차단됨', unbanSuccess: '차단 해제됨',
                liveChat: '실시간 채팅', chatOnline: '온라인', chatTyping: '입력 중...', chatTypingMultiple: '입력 중...',
                chatSend: '전송', chatPlaceholder: '메시지 입력...', chatNoMessages: '메시지가 없습니다. 대화를 시작하세요!',
                chatSearchGif: 'GIF 검색...', chatTrending: '인기', chatPoweredBy: 'Tenor 제공',
                chatDeleted: '메시지 삭제됨', chatYou: '나', chatJustNow: '방금', chatMinutesAgo: '분 전', chatHoursAgo: '시간 전', chatYesterday: '어제',
                chatModerators: '관리자', chatAddMod: '관리자 추가', chatRemoveMod: '제거', chatBannedUsers: '차단된 사용자',
                chatBan: '차단', chatUnban: '차단 해제', chatMute: '음소거', chatBanFromMedia: '미디어 차단', chatMinutes: '분',
                chatBanned: '채팅이 차단되었습니다', chatMuted: '일시적으로 음소거되었습니다', chatRateLimited: '잠시만요! 메시지가 너무 많습니다.',
                chatConnecting: '연결 중...', chatReconnecting: '재연결 중...', chatDisconnected: '연결 끊김',
                chatClearAll: '모두 삭제', chatClearConfirm: '모든 메시지를 삭제하시겠습니까? 이 작업은 취소할 수 없습니다.', chatCleared: '채팅이 삭제됨'
            },
            it: {
                requestMedia: 'Richiedi Media', manageRequests: 'Gestisci Richieste', requestDescription: '📬 Richiedi i tuoi Media Preferiti!',
                requestDescriptionText: 'Usa questo modulo per richiedere film o serie TV che vorresti guardare.',
                mediaTitle: 'Titolo *', mediaTitlePlaceholder: 'es., Breaking Bad, Il Padrino', type: 'Tipo *', selectType: '-- Seleziona Tipo --',
                movie: 'Film', tvSeries: 'Serie TV', anime: 'Anime', documentary: 'Documentario', other: 'Altro',
                additionalNotes: 'Note Aggiuntive', notesPlaceholder: 'Numero stagione, anno, dettagli specifici, ecc.', submitRequest: 'Invia Richiesta',
                yourRequests: 'Le Tue Richieste', loadingRequests: 'Caricamento richieste...', noRequests: 'Non hai ancora richiesto nessun media',
                errorLoading: 'Errore di caricamento', notSpecified: 'Non specificato', noDetails: 'Nessun dettaglio', watchNow: '🎬 Guarda Ora',
                noRequestsYet: 'Nessuna richiesta ancora', newRequest: 'Nuova Richiesta', pending: 'IN ATTESA', processing: 'IN ELABORAZIONE', done: 'COMPLETATO', rejected: 'RIFIUTATO',
                titleRequired: 'Inserisci un titolo', typeRequired: 'Seleziona un tipo', requestSubmitted: 'Richiesta inviata con successo!',
                requestFailed: 'Invio fallito', statusUpdated: 'Stato aggiornato', statusUpdateFailed: 'Aggiornamento stato fallito',
                addLink: '+ Link', enterMediaLink: 'Inserisci link:', delete: 'Elimina', confirmDelete: 'Sei sicuro di voler eliminare questa richiesta?',
                mediaLinkPlaceholder: 'Link del media', unknown: 'Sconosciuto', loading: 'Caricamento...',
                snooze: 'Posticipa', unsnooze: 'Riattiva', snoozed: 'POSTICIPATO', snoozedUntil: 'Posticipato fino a', snoozeDate: 'Posticipa fino a',
                categoryNew: '🆕 Nuovo', categoryProcessing: '🔄 In elaborazione', categoryPending: '⏳ In attesa', categorySnoozed: '💤 Posticipato', categoryDone: '✅ Completato', categoryRejected: '❌ Rifiutato',
                createRequest: 'Crea Richiesta', latestMedia: 'Ultimi Media', latestMediaLoading: 'Caricamento...', latestMediaEmpty: 'Nessun media recente', latestMediaError: 'Caricamento fallito',
                newEpisode: '+1 episodio', newEpisodes: '+{count} episodi', typeMovie: 'Film', typeSeries: 'Serie', typeAnime: 'Anime', typeOther: 'Altro',
                timeAgo: 'fa', timeJustNow: 'adesso', timeMinutes: 'min', timeHours: 'h', timeDays: 'g',
                mediaManagement: 'Media', mediaManagementTitle: 'Gestione Media', mediaSearch: 'Cerca...', mediaTypeAll: 'Tutti i Tipi', mediaTypeMovie: 'Film', mediaTypeSeries: 'Serie',
                mediaSortBy: 'Ordina per', mediaSortTitle: 'Titolo', mediaSortYear: 'Anno', mediaSortRating: 'Valutazione', mediaSortPlays: 'Riproduzioni', mediaSortSize: 'Dimensione',
                mediaSortDateAdded: 'Data Aggiunta', mediaSortPlayCount: 'Riproduzioni', mediaSortWatchTime: 'Tempo Guardato', mediaLoading: 'Caricamento...', mediaNoResults: 'Nessun media', mediaError: 'Errore caricamento',
                mediaScheduleDelete: 'Pianifica Eliminazione', mediaCancelDelete: 'Annulla Eliminazione', mediaDeleteIn: 'Elimina tra', mediaLeavingIn: 'Rimozione tra',
                media1Day: '1 Giorno', media3Days: '3 Giorni', media1Week: '1 Settimana', media2Weeks: '2 Settimane', mediaCustom: 'Personalizzato...', mediaCustomHours: 'Ore',
                mediaSchedule: 'Pianifica', mediaCancel: 'Annulla', mediaNoScheduled: 'Nessuna eliminazione pianificata', mediaScheduledBy: 'Pianificato da', mediaDeletesIn: 'Eliminazione tra',
                mediaActions: 'Azioni', mediaChange: 'Modifica', mediaChangeTime: 'Modifica tempo', mediaSoon: 'Presto', mediaDays: 'giorni', mediaPlays: 'riproduzioni',
                mediaMinutes: 'min', mediaGB: 'GB', mediaMB: 'MB', mediaPage: 'Pagina', mediaOf: 'di', mediaPrev: 'Prec', mediaNext: 'Succ', mediaGo: 'Vai',
                mediaTypeScheduled: 'Pianificati', mediaSettings: 'Impostazioni', mediaIncludeTypes: 'Includi tipi:', mediaTypesHint: 'Seleziona i tipi da mostrare',
                requestDeleteRequest: 'Richiedi eliminazione', requestDeleteMedia: 'Richiedi eliminazione media', deletionRequests: 'Richieste di Eliminazione', noDeletionRequests: 'Nessuna richiesta di eliminazione',
                deleteNow: 'Elimina ~1h', schedule1Day: '1 Giorno', schedule1Week: '1 Settimana', schedule1Month: '1 Mese', rejectDeletion: 'Rifiuta', approveDeleteRequest: 'Approva',
                alreadyRequested: 'Eliminazione Richiesta', deletionApproved: 'APPROVATO', deletionRejected: 'RIFIUTATO', deletionPending: 'IN ATTESA',
                deletionRequestSent: 'Richiesta eliminazione inviata!', deletionRequestFailed: 'Invio fallito', deletionActionFailed: 'Elaborazione fallita',
                deleteRequest: 'Elimina Richiesta', deleteMedia: 'Elimina Media', rejectionReasonPrompt: 'Motivo del rifiuto (opzionale):', rejectionReasonLabel: 'Motivo:',
                deletionLimitReached: 'Limite richieste raggiunto', banUser: 'Banna', unbanUser: 'Sbanna', bannedUsers: 'Utenti Bannati',
                ban1Day: '1 Giorno', ban1Week: '1 Settimana', ban1Month: '1 Mese', banPermanent: 'Permanente', banExpires: 'Scade:', banPermanentLabel: 'Permanente',
                bannedBy: 'da', noBannedUsers: 'Nessun utente bannato', youAreBanned: 'Sei bannato da questa azione', banSuccess: 'Utente bannato', unbanSuccess: 'Utente sbannato',
                liveChat: 'Chat dal Vivo', chatOnline: 'online', chatTyping: 'sta scrivendo...', chatTypingMultiple: 'stanno scrivendo...',
                chatSend: 'Invia', chatPlaceholder: 'Scrivi un messaggio...', chatNoMessages: 'Nessun messaggio. Inizia la conversazione!',
                chatSearchGif: 'Cerca GIF...', chatTrending: 'Tendenze', chatPoweredBy: 'Powered by Tenor',
                chatDeleted: 'Messaggio eliminato', chatYou: 'Tu', chatJustNow: 'Adesso', chatMinutesAgo: 'min', chatHoursAgo: 'h', chatYesterday: 'Ieri',
                chatModerators: 'Moderatori', chatAddMod: 'Aggiungi Moderatore', chatRemoveMod: 'Rimuovi', chatBannedUsers: 'Utenti Bannati',
                chatBan: 'Banna', chatUnban: 'Sbanna', chatMute: 'Silenzia', chatBanFromMedia: 'Banna dai Media', chatMinutes: 'minuti',
                chatBanned: 'Sei bannato dalla chat', chatMuted: 'Sei temporaneamente silenziato', chatRateLimited: 'Piano! Troppi messaggi.',
                chatConnecting: 'Connessione...', chatReconnecting: 'Riconnessione...', chatDisconnected: 'Disconnesso',
                chatClearAll: 'Cancella tutto', chatClearConfirm: 'Eliminare tutti i messaggi? Non può essere annullato.', chatCleared: 'Chat cancellata'
            },
            tr: {
                requestMedia: 'Medya İste', manageRequests: 'İstekleri Yönet', requestDescription: '📬 Favori Medyanızı İsteyin!',
                requestDescriptionText: 'İzlemek istediğiniz film veya dizileri istemek için bu formu kullanın.',
                mediaTitle: 'Medya Başlığı *', mediaTitlePlaceholder: 'ör., Breaking Bad, Baba', type: 'Tür *', selectType: '-- Tür Seçin --',
                movie: 'Film', tvSeries: 'TV Dizisi', anime: 'Anime', documentary: 'Belgesel', other: 'Diğer',
                additionalNotes: 'Ek Notlar', notesPlaceholder: 'Sezon numarası, yıl, özel detaylar, vb.', submitRequest: 'İsteği Gönder',
                yourRequests: 'İstekleriniz', loadingRequests: 'İstekleriniz yükleniyor...', noRequests: 'Henüz medya istemediniz',
                errorLoading: 'Yükleme hatası', notSpecified: 'Belirtilmemiş', noDetails: 'Detay yok', watchNow: '🎬 Şimdi İzle',
                noRequestsYet: 'Henüz istek yok', newRequest: 'Yeni İstek', pending: 'BEKLİYOR', processing: 'İŞLENİYOR', done: 'TAMAMLANDI', rejected: 'REDDEDİLDİ',
                titleRequired: 'Lütfen başlık girin', typeRequired: 'Lütfen tür seçin', requestSubmitted: 'İstek başarıyla gönderildi!',
                requestFailed: 'İstek gönderilemedi', statusUpdated: 'Durum güncellendi', statusUpdateFailed: 'Durum güncellenemedi',
                addLink: '+ Link', enterMediaLink: 'Link girin:', delete: 'Sil', confirmDelete: 'Bu isteği silmek istediğinizden emin misiniz?',
                mediaLinkPlaceholder: 'Medya linki', unknown: 'Bilinmiyor', loading: 'Yükleniyor...',
                snooze: 'Ertele', unsnooze: 'Yeniden Etkinleştir', snoozed: 'ERTELENDİ', snoozedUntil: 'Ertelendi', snoozeDate: 'Erteleme tarihi',
                categoryNew: '🆕 Yeni', categoryProcessing: '🔄 İşleniyor', categoryPending: '⏳ Bekliyor', categorySnoozed: '💤 Ertelendi', categoryDone: '✅ Tamamlandı', categoryRejected: '❌ Reddedildi',
                createRequest: 'İstek Oluştur', latestMedia: 'Son Medya', latestMediaLoading: 'Yükleniyor...', latestMediaEmpty: 'Son medya yok', latestMediaError: 'Yükleme başarısız',
                newEpisode: '+1 bölüm', newEpisodes: '+{count} bölüm', typeMovie: 'Film', typeSeries: 'Dizi', typeAnime: 'Anime', typeOther: 'Diğer',
                timeAgo: 'önce', timeJustNow: 'şimdi', timeMinutes: 'dk', timeHours: 'sa', timeDays: 'g',
                mediaManagement: 'Medya', mediaManagementTitle: 'Medya Yönetimi', mediaSearch: 'Ara...', mediaTypeAll: 'Tüm Türler', mediaTypeMovie: 'Filmler', mediaTypeSeries: 'Diziler',
                mediaSortBy: 'Sırala', mediaSortTitle: 'Başlık', mediaSortYear: 'Yıl', mediaSortRating: 'Puan', mediaSortPlays: 'Oynatma', mediaSortSize: 'Boyut',
                mediaSortDateAdded: 'Eklenme Tarihi', mediaSortPlayCount: 'Oynatma', mediaSortWatchTime: 'İzleme Süresi', mediaLoading: 'Yükleniyor...', mediaNoResults: 'Medya bulunamadı', mediaError: 'Yükleme hatası',
                mediaScheduleDelete: 'Silme Planla', mediaCancelDelete: 'Silmeyi İptal', mediaDeleteIn: 'Silinecek', mediaLeavingIn: 'Kaldırılacak',
                media1Day: '1 Gün', media3Days: '3 Gün', media1Week: '1 Hafta', media2Weeks: '2 Hafta', mediaCustom: 'Özel...', mediaCustomHours: 'Saat',
                mediaSchedule: 'Planla', mediaCancel: 'İptal', mediaNoScheduled: 'Planlanan silme yok', mediaScheduledBy: 'Planlayan', mediaDeletesIn: 'Silinecek',
                mediaActions: 'İşlemler', mediaChange: 'Değiştir', mediaChangeTime: 'Zamanı değiştir', mediaSoon: 'Yakında', mediaDays: 'gün', mediaPlays: 'oynatma',
                mediaMinutes: 'dk', mediaGB: 'GB', mediaMB: 'MB', mediaPage: 'Sayfa', mediaOf: '/', mediaPrev: 'Önceki', mediaNext: 'Sonraki', mediaGo: 'Git',
                mediaTypeScheduled: 'Planlanmış', mediaSettings: 'Ayarlar', mediaIncludeTypes: 'Türleri dahil et:', mediaTypesHint: 'Gösterilecek türleri seçin',
                requestDeleteRequest: 'Silme iste', requestDeleteMedia: 'Medya silme iste', deletionRequests: 'Silme İstekleri', noDeletionRequests: 'Silme isteği yok',
                deleteNow: 'Şimdi sil', schedule1Day: '1 Gün', schedule1Week: '1 Hafta', schedule1Month: '1 Ay', rejectDeletion: 'Reddet', approveDeleteRequest: 'Onayla',
                alreadyRequested: 'Silme İstendi', deletionApproved: 'ONAYLANDI', deletionRejected: 'REDDEDİLDİ', deletionPending: 'BEKLİYOR',
                deletionRequestSent: 'Silme isteği gönderildi!', deletionRequestFailed: 'Gönderme başarısız', deletionActionFailed: 'İşlem başarısız',
                deleteRequest: 'İsteği Sil', deleteMedia: 'Medyayı Sil', rejectionReasonPrompt: 'Red nedeni (isteğe bağlı):', rejectionReasonLabel: 'Neden:',
                deletionLimitReached: 'İstek sınırına ulaşıldı', banUser: 'Yasakla', unbanUser: 'Yasağı Kaldır', bannedUsers: 'Yasaklı Kullanıcılar',
                ban1Day: '1 Gün', ban1Week: '1 Hafta', ban1Month: '1 Ay', banPermanent: 'Kalıcı', banExpires: 'Bitiş:', banPermanentLabel: 'Kalıcı',
                bannedBy: 'tarafından', noBannedUsers: 'Yasaklı kullanıcı yok', youAreBanned: 'Bu işlem için yasaklısınız', banSuccess: 'Kullanıcı yasaklandı', unbanSuccess: 'Yasak kaldırıldı',
                liveChat: 'Canlı Sohbet', chatOnline: 'çevrimiçi', chatTyping: 'yazıyor...', chatTypingMultiple: 'yazıyorlar...',
                chatSend: 'Gönder', chatPlaceholder: 'Mesaj yazın...', chatNoMessages: 'Mesaj yok. Sohbeti başlatın!',
                chatSearchGif: 'GIF Ara...', chatTrending: 'Trendler', chatPoweredBy: 'Tenor ile desteklenir',
                chatDeleted: 'Mesaj silindi', chatYou: 'Sen', chatJustNow: 'Şimdi', chatMinutesAgo: 'dk', chatHoursAgo: 'sa', chatYesterday: 'Dün',
                chatModerators: 'Moderatörler', chatAddMod: 'Moderatör Ekle', chatRemoveMod: 'Kaldır', chatBannedUsers: 'Yasaklı Kullanıcılar',
                chatBan: 'Yasakla', chatUnban: 'Yasağı Kaldır', chatMute: 'Sustur', chatBanFromMedia: 'Medyadan Yasakla', chatMinutes: 'dakika',
                chatBanned: 'Sohbetten yasaklandınız', chatMuted: 'Geçici olarak susturuldunuz', chatRateLimited: 'Yavaş olun! Çok fazla mesaj.',
                chatConnecting: 'Bağlanıyor...', chatReconnecting: 'Yeniden bağlanıyor...', chatDisconnected: 'Bağlantı kesildi',
                chatClearAll: 'Tümünü Temizle', chatClearConfirm: 'Tüm mesajları sil? Bu geri alınamaz.', chatCleared: 'Sohbet temizlendi'
            },
            pl: {
                requestMedia: 'Poproś o Media', manageRequests: 'Zarządzaj Prośbami', requestDescription: '📬 Poproś o Ulubione Media!',
                requestDescriptionText: 'Użyj tego formularza, aby poprosić o filmy lub seriale, które chciałbyś obejrzeć.',
                mediaTitle: 'Tytuł *', mediaTitlePlaceholder: 'np., Breaking Bad, Ojciec Chrzestny', type: 'Typ *', selectType: '-- Wybierz Typ --',
                movie: 'Film', tvSeries: 'Serial TV', anime: 'Anime', documentary: 'Dokument', other: 'Inne',
                additionalNotes: 'Dodatkowe Uwagi', notesPlaceholder: 'Numer sezonu, rok, szczegóły, itp.', submitRequest: 'Wyślij Prośbę',
                yourRequests: 'Twoje Prośby', loadingRequests: 'Ładowanie próśb...', noRequests: 'Nie masz jeszcze żadnych próśb',
                errorLoading: 'Błąd ładowania', notSpecified: 'Nieokreślone', noDetails: 'Brak szczegółów', watchNow: '🎬 Oglądaj Teraz',
                noRequestsYet: 'Brak próśb', newRequest: 'Nowa Prośba', pending: 'OCZEKUJE', processing: 'PRZETWARZANIE', done: 'GOTOWE', rejected: 'ODRZUCONE',
                titleRequired: 'Proszę podać tytuł', typeRequired: 'Proszę wybrać typ', requestSubmitted: 'Prośba wysłana pomyślnie!',
                requestFailed: 'Nie udało się wysłać', statusUpdated: 'Status zaktualizowany', statusUpdateFailed: 'Nie udało się zaktualizować statusu',
                addLink: '+ Link', enterMediaLink: 'Wprowadź link:', delete: 'Usuń', confirmDelete: 'Czy na pewno chcesz usunąć tę prośbę?',
                mediaLinkPlaceholder: 'Link do mediów', unknown: 'Nieznane', loading: 'Ładowanie...',
                snooze: 'Odłóż', unsnooze: 'Wznów', snoozed: 'ODŁOŻONE', snoozedUntil: 'Odłożone do', snoozeDate: 'Odłóż do daty',
                categoryNew: '🆕 Nowe', categoryProcessing: '🔄 Przetwarzanie', categoryPending: '⏳ Oczekujące', categorySnoozed: '💤 Odłożone', categoryDone: '✅ Gotowe', categoryRejected: '❌ Odrzucone',
                createRequest: 'Utwórz Prośbę', latestMedia: 'Najnowsze Media', latestMediaLoading: 'Ładowanie...', latestMediaEmpty: 'Brak nowych mediów', latestMediaError: 'Błąd ładowania',
                newEpisode: '+1 odcinek', newEpisodes: '+{count} odcinków', typeMovie: 'Film', typeSeries: 'Serial', typeAnime: 'Anime', typeOther: 'Inne',
                timeAgo: 'temu', timeJustNow: 'przed chwilą', timeMinutes: 'min', timeHours: 'godz', timeDays: 'dni',
                mediaManagement: 'Media', mediaManagementTitle: 'Zarządzanie Mediami', mediaSearch: 'Szukaj...', mediaTypeAll: 'Wszystkie Typy', mediaTypeMovie: 'Filmy', mediaTypeSeries: 'Seriale',
                mediaSortBy: 'Sortuj wg', mediaSortTitle: 'Tytuł', mediaSortYear: 'Rok', mediaSortRating: 'Ocena', mediaSortPlays: 'Odtworzenia', mediaSortSize: 'Rozmiar',
                mediaSortDateAdded: 'Data Dodania', mediaSortPlayCount: 'Odtworzenia', mediaSortWatchTime: 'Czas Oglądania', mediaLoading: 'Ładowanie...', mediaNoResults: 'Brak mediów', mediaError: 'Błąd ładowania',
                mediaScheduleDelete: 'Zaplanuj Usunięcie', mediaCancelDelete: 'Anuluj Usunięcie', mediaDeleteIn: 'Usuń za', mediaLeavingIn: 'Usunięcie za',
                media1Day: '1 Dzień', media3Days: '3 Dni', media1Week: '1 Tydzień', media2Weeks: '2 Tygodnie', mediaCustom: 'Własne...', mediaCustomHours: 'Godziny',
                mediaSchedule: 'Zaplanuj', mediaCancel: 'Anuluj', mediaNoScheduled: 'Brak zaplanowanych usunięć', mediaScheduledBy: 'Zaplanowane przez', mediaDeletesIn: 'Usunięcie za',
                mediaActions: 'Akcje', mediaChange: 'Zmień', mediaChangeTime: 'Zmień czas', mediaSoon: 'Wkrótce', mediaDays: 'dni', mediaPlays: 'odtworzeń',
                mediaMinutes: 'min', mediaGB: 'GB', mediaMB: 'MB', mediaPage: 'Strona', mediaOf: 'z', mediaPrev: 'Poprz', mediaNext: 'Nast', mediaGo: 'Idź',
                mediaTypeScheduled: 'Zaplanowane', mediaSettings: 'Ustawienia', mediaIncludeTypes: 'Uwzględnij typy:', mediaTypesHint: 'Wybierz typy do wyświetlenia',
                requestDeleteRequest: 'Poproś o usunięcie', requestDeleteMedia: 'Poproś o usunięcie mediów', deletionRequests: 'Prośby o Usunięcie', noDeletionRequests: 'Brak próśb o usunięcie',
                deleteNow: 'Usuń ~1h', schedule1Day: '1 Dzień', schedule1Week: '1 Tydzień', schedule1Month: '1 Miesiąc', rejectDeletion: 'Odrzuć', approveDeleteRequest: 'Zatwierdź',
                alreadyRequested: 'Usunięcie Zgłoszone', deletionApproved: 'ZATWIERDZONE', deletionRejected: 'ODRZUCONE', deletionPending: 'OCZEKUJE',
                deletionRequestSent: 'Prośba o usunięcie wysłana!', deletionRequestFailed: 'Nie udało się wysłać', deletionActionFailed: 'Nie udało się przetworzyć',
                deleteRequest: 'Usuń Prośbę', deleteMedia: 'Usuń Media', rejectionReasonPrompt: 'Powód odrzucenia (opcjonalnie):', rejectionReasonLabel: 'Powód:',
                deletionLimitReached: 'Osiągnięto limit próśb', banUser: 'Zbanuj', unbanUser: 'Odbanuj', bannedUsers: 'Zbanowani Użytkownicy',
                ban1Day: '1 Dzień', ban1Week: '1 Tydzień', ban1Month: '1 Miesiąc', banPermanent: 'Na stałe', banExpires: 'Wygasa:', banPermanentLabel: 'Na stałe',
                bannedBy: 'przez', noBannedUsers: 'Brak zbanowanych użytkowników', youAreBanned: 'Jesteś zbanowany z tej akcji', banSuccess: 'Użytkownik zbanowany', unbanSuccess: 'Użytkownik odbanowany',
                liveChat: 'Czat na żywo', chatOnline: 'online', chatTyping: 'pisze...', chatTypingMultiple: 'piszą...',
                chatSend: 'Wyślij', chatPlaceholder: 'Napisz wiadomość...', chatNoMessages: 'Brak wiadomości. Rozpocznij rozmowę!',
                chatSearchGif: 'Szukaj GIF...', chatTrending: 'Popularne', chatPoweredBy: 'Obsługiwane przez Tenor',
                chatDeleted: 'Wiadomość usunięta', chatYou: 'Ty', chatJustNow: 'Teraz', chatMinutesAgo: 'min', chatHoursAgo: 'godz', chatYesterday: 'Wczoraj',
                chatModerators: 'Moderatorzy', chatAddMod: 'Dodaj Moderatora', chatRemoveMod: 'Usuń', chatBannedUsers: 'Zbanowani Użytkownicy',
                chatBan: 'Zbanuj', chatUnban: 'Odbanuj', chatMute: 'Wycisz', chatBanFromMedia: 'Zbanuj z Mediów', chatMinutes: 'minut',
                chatBanned: 'Jesteś zbanowany z czatu', chatMuted: 'Jesteś tymczasowo wyciszony', chatRateLimited: 'Zwolnij! Za dużo wiadomości.',
                chatConnecting: 'Łączenie...', chatReconnecting: 'Ponowne łączenie...', chatDisconnected: 'Rozłączono',
                chatClearAll: 'Wyczyść wszystko', chatClearConfirm: 'Usunąć wszystkie wiadomości? Tej operacji nie można cofnąć.', chatCleared: 'Czat wyczyszczony'
            },
            nl: {
                requestMedia: 'Media Aanvragen', manageRequests: 'Verzoeken Beheren', requestDescription: '📬 Vraag je Favoriete Media Aan!',
                requestDescriptionText: 'Gebruik dit formulier om films of series aan te vragen die je wilt bekijken.',
                mediaTitle: 'Titel *', mediaTitlePlaceholder: 'bijv., Breaking Bad, The Godfather', type: 'Type *', selectType: '-- Selecteer Type --',
                movie: 'Film', tvSeries: 'TV Serie', anime: 'Anime', documentary: 'Documentaire', other: 'Anders',
                additionalNotes: 'Extra Opmerkingen', notesPlaceholder: 'Seizoensnummer, jaar, specifieke details, etc.', submitRequest: 'Verzoek Indienen',
                yourRequests: 'Jouw Verzoeken', loadingRequests: 'Verzoeken laden...', noRequests: 'Je hebt nog geen media aangevraagd',
                errorLoading: 'Fout bij laden', notSpecified: 'Niet opgegeven', noDetails: 'Geen details', watchNow: '🎬 Nu Bekijken',
                noRequestsYet: 'Nog geen verzoeken', newRequest: 'Nieuw Verzoek', pending: 'WACHTEND', processing: 'BEZIG', done: 'KLAAR', rejected: 'AFGEWEZEN',
                titleRequired: 'Voer een titel in', typeRequired: 'Selecteer een type', requestSubmitted: 'Verzoek succesvol ingediend!',
                requestFailed: 'Verzoek mislukt', statusUpdated: 'Status bijgewerkt', statusUpdateFailed: 'Status update mislukt',
                addLink: '+ Link', enterMediaLink: 'Voer link in:', delete: 'Verwijderen', confirmDelete: 'Weet je zeker dat je dit verzoek wilt verwijderen?',
                mediaLinkPlaceholder: 'Media link', unknown: 'Onbekend', loading: 'Laden...',
                snooze: 'Uitstellen', unsnooze: 'Heractiveren', snoozed: 'UITGESTELD', snoozedUntil: 'Uitgesteld tot', snoozeDate: 'Uitstellen tot',
                categoryNew: '🆕 Nieuw', categoryProcessing: '🔄 Bezig', categoryPending: '⏳ Wachtend', categorySnoozed: '💤 Uitgesteld', categoryDone: '✅ Klaar', categoryRejected: '❌ Afgewezen',
                createRequest: 'Verzoek Maken', latestMedia: 'Laatste Media', latestMediaLoading: 'Laden...', latestMediaEmpty: 'Geen recente media', latestMediaError: 'Laden mislukt',
                newEpisode: '+1 aflevering', newEpisodes: '+{count} afleveringen', typeMovie: 'Film', typeSeries: 'Serie', typeAnime: 'Anime', typeOther: 'Anders',
                timeAgo: 'geleden', timeJustNow: 'zojuist', timeMinutes: 'min', timeHours: 'u', timeDays: 'd',
                mediaManagement: 'Media', mediaManagementTitle: 'Mediabeheer', mediaSearch: 'Zoeken...', mediaTypeAll: 'Alle Types', mediaTypeMovie: 'Films', mediaTypeSeries: 'Series',
                mediaSortBy: 'Sorteren op', mediaSortTitle: 'Titel', mediaSortYear: 'Jaar', mediaSortRating: 'Beoordeling', mediaSortPlays: 'Afgespeeld', mediaSortSize: 'Grootte',
                mediaSortDateAdded: 'Toegevoegd', mediaSortPlayCount: 'Afgespeeld', mediaSortWatchTime: 'Kijktijd', mediaLoading: 'Laden...', mediaNoResults: 'Geen media', mediaError: 'Laadfout',
                mediaScheduleDelete: 'Verwijdering Plannen', mediaCancelDelete: 'Verwijdering Annuleren', mediaDeleteIn: 'Verwijderen over', mediaLeavingIn: 'Verdwijnt over',
                media1Day: '1 Dag', media3Days: '3 Dagen', media1Week: '1 Week', media2Weeks: '2 Weken', mediaCustom: 'Aangepast...', mediaCustomHours: 'Uren',
                mediaSchedule: 'Plannen', mediaCancel: 'Annuleren', mediaNoScheduled: 'Geen geplande verwijderingen', mediaScheduledBy: 'Gepland door', mediaDeletesIn: 'Verwijderd over',
                mediaActions: 'Acties', mediaChange: 'Wijzigen', mediaChangeTime: 'Tijd wijzigen', mediaSoon: 'Binnenkort', mediaDays: 'dagen', mediaPlays: 'keer afgespeeld',
                mediaMinutes: 'min', mediaGB: 'GB', mediaMB: 'MB', mediaPage: 'Pagina', mediaOf: 'van', mediaPrev: 'Vorige', mediaNext: 'Volgende', mediaGo: 'Ga',
                mediaTypeScheduled: 'Gepland', mediaSettings: 'Instellingen', mediaIncludeTypes: 'Inclusief types:', mediaTypesHint: 'Selecteer te tonen types',
                requestDeleteRequest: 'Verwijdering aanvragen', requestDeleteMedia: 'Media verwijdering aanvragen', deletionRequests: 'Verwijderverzoeken', noDeletionRequests: 'Geen verwijderverzoeken',
                deleteNow: 'Nu verwijderen', schedule1Day: '1 Dag', schedule1Week: '1 Week', schedule1Month: '1 Maand', rejectDeletion: 'Afwijzen', approveDeleteRequest: 'Goedkeuren',
                alreadyRequested: 'Verwijdering Aangevraagd', deletionApproved: 'GOEDGEKEURD', deletionRejected: 'AFGEWEZEN', deletionPending: 'WACHTEND',
                deletionRequestSent: 'Verwijderverzoek verzonden!', deletionRequestFailed: 'Verzenden mislukt', deletionActionFailed: 'Verwerking mislukt',
                deleteRequest: 'Verzoek Verwijderen', deleteMedia: 'Media Verwijderen', rejectionReasonPrompt: 'Reden afwijzing (optioneel):', rejectionReasonLabel: 'Reden:',
                deletionLimitReached: 'Verzoeklimiet bereikt', banUser: 'Blokkeren', unbanUser: 'Deblokkeren', bannedUsers: 'Geblokkeerde Gebruikers',
                ban1Day: '1 Dag', ban1Week: '1 Week', ban1Month: '1 Maand', banPermanent: 'Permanent', banExpires: 'Verloopt:', banPermanentLabel: 'Permanent',
                bannedBy: 'door', noBannedUsers: 'Geen geblokkeerde gebruikers', youAreBanned: 'Je bent geblokkeerd voor deze actie', banSuccess: 'Gebruiker geblokkeerd', unbanSuccess: 'Gebruiker gedeblokkeerd',
                liveChat: 'Live Chat', chatOnline: 'online', chatTyping: 'typt...', chatTypingMultiple: 'typen...',
                chatSend: 'Verstuur', chatPlaceholder: 'Typ een bericht...', chatNoMessages: 'Geen berichten. Start het gesprek!',
                chatSearchGif: 'GIFs zoeken...', chatTrending: 'Trending', chatPoweredBy: 'Mogelijk gemaakt door Tenor',
                chatDeleted: 'Bericht verwijderd', chatYou: 'Jij', chatJustNow: 'Zojuist', chatMinutesAgo: 'min', chatHoursAgo: 'u', chatYesterday: 'Gisteren',
                chatModerators: 'Moderators', chatAddMod: 'Moderator Toevoegen', chatRemoveMod: 'Verwijderen', chatBannedUsers: 'Geblokkeerde Gebruikers',
                chatBan: 'Blokkeren', chatUnban: 'Deblokkeren', chatMute: 'Dempen', chatBanFromMedia: 'Blokkeren van Media', chatMinutes: 'minuten',
                chatBanned: 'Je bent geblokkeerd van de chat', chatMuted: 'Je bent tijdelijk gedempt', chatRateLimited: 'Rustig aan! Te veel berichten.',
                chatConnecting: 'Verbinden...', chatReconnecting: 'Opnieuw verbinden...', chatDisconnected: 'Verbinding verbroken',
                chatClearAll: 'Alles wissen', chatClearConfirm: 'Alle berichten verwijderen? Dit kan niet ongedaan worden gemaakt.', chatCleared: 'Chat gewist'
            },
            ar: {
                requestMedia: 'طلب وسائط', manageRequests: 'إدارة الطلبات', requestDescription: '📬 اطلب وسائطك المفضلة!',
                requestDescriptionText: 'استخدم هذا النموذج لطلب الأفلام أو المسلسلات التي ترغب في مشاهدتها.',
                mediaTitle: 'العنوان *', mediaTitlePlaceholder: 'مثال: بريكنج باد، العراب', type: 'النوع *', selectType: '-- اختر النوع --',
                movie: 'فيلم', tvSeries: 'مسلسل', anime: 'أنمي', documentary: 'وثائقي', other: 'أخرى',
                additionalNotes: 'ملاحظات إضافية', notesPlaceholder: 'رقم الموسم، السنة، تفاصيل محددة، إلخ.', submitRequest: 'إرسال الطلب',
                yourRequests: 'طلباتك', loadingRequests: 'جاري تحميل طلباتك...', noRequests: 'لم تطلب أي وسائط بعد',
                errorLoading: 'خطأ في التحميل', notSpecified: 'غير محدد', noDetails: 'لا توجد تفاصيل', watchNow: '🎬 شاهد الآن',
                noRequestsYet: 'لا توجد طلبات بعد', newRequest: 'طلب جديد', pending: 'قيد الانتظار', processing: 'قيد المعالجة', done: 'مكتمل', rejected: 'مرفوض',
                titleRequired: 'الرجاء إدخال العنوان', typeRequired: 'الرجاء اختيار النوع', requestSubmitted: 'تم إرسال الطلب بنجاح!',
                requestFailed: 'فشل إرسال الطلب', statusUpdated: 'تم تحديث الحالة', statusUpdateFailed: 'فشل تحديث الحالة',
                addLink: '+ رابط', enterMediaLink: 'أدخل الرابط:', delete: 'حذف', confirmDelete: 'هل أنت متأكد من حذف هذا الطلب؟',
                mediaLinkPlaceholder: 'رابط الوسائط', unknown: 'غير معروف', loading: 'جاري التحميل...',
                snooze: 'تأجيل', unsnooze: 'إلغاء التأجيل', snoozed: 'مؤجل', snoozedUntil: 'مؤجل حتى', snoozeDate: 'تأجيل حتى',
                categoryNew: '🆕 جديد', categoryProcessing: '🔄 قيد المعالجة', categoryPending: '⏳ قيد الانتظار', categorySnoozed: '💤 مؤجل', categoryDone: '✅ مكتمل', categoryRejected: '❌ مرفوض',
                createRequest: 'إنشاء طلب', latestMedia: 'أحدث الوسائط', latestMediaLoading: 'جاري التحميل...', latestMediaEmpty: 'لا توجد وسائط حديثة', latestMediaError: 'فشل التحميل',
                newEpisode: '+1 حلقة', newEpisodes: '+{count} حلقات', typeMovie: 'فيلم', typeSeries: 'مسلسل', typeAnime: 'أنمي', typeOther: 'أخرى',
                timeAgo: 'مضت', timeJustNow: 'الآن', timeMinutes: 'دقيقة', timeHours: 'ساعة', timeDays: 'يوم',
                mediaManagement: 'الوسائط', mediaManagementTitle: 'إدارة الوسائط', mediaSearch: 'بحث...', mediaTypeAll: 'جميع الأنواع', mediaTypeMovie: 'أفلام', mediaTypeSeries: 'مسلسلات',
                mediaSortBy: 'ترتيب حسب', mediaSortTitle: 'العنوان', mediaSortYear: 'السنة', mediaSortRating: 'التقييم', mediaSortPlays: 'المشاهدات', mediaSortSize: 'الحجم',
                mediaSortDateAdded: 'تاريخ الإضافة', mediaSortPlayCount: 'المشاهدات', mediaSortWatchTime: 'وقت المشاهدة', mediaLoading: 'جاري التحميل...', mediaNoResults: 'لا توجد وسائط', mediaError: 'خطأ في التحميل',
                mediaScheduleDelete: 'جدولة الحذف', mediaCancelDelete: 'إلغاء الحذف', mediaDeleteIn: 'حذف خلال', mediaLeavingIn: 'سيُحذف خلال',
                media1Day: '1 يوم', media3Days: '3 أيام', media1Week: '1 أسبوع', media2Weeks: '2 أسابيع', mediaCustom: 'مخصص...', mediaCustomHours: 'ساعات',
                mediaSchedule: 'جدولة', mediaCancel: 'إلغاء', mediaNoScheduled: 'لا يوجد حذف مجدول', mediaScheduledBy: 'مجدول بواسطة', mediaDeletesIn: 'يُحذف خلال',
                mediaActions: 'إجراءات', mediaChange: 'تغيير', mediaChangeTime: 'تغيير الوقت', mediaSoon: 'قريباً', mediaDays: 'أيام', mediaPlays: 'مشاهدات',
                mediaMinutes: 'دقيقة', mediaGB: 'جيجا', mediaMB: 'ميجا', mediaPage: 'صفحة', mediaOf: 'من', mediaPrev: 'السابق', mediaNext: 'التالي', mediaGo: 'انتقل',
                mediaTypeScheduled: 'مجدول', mediaSettings: 'الإعدادات', mediaIncludeTypes: 'تضمين الأنواع:', mediaTypesHint: 'اختر الأنواع للعرض',
                requestDeleteRequest: 'طلب حذف', requestDeleteMedia: 'طلب حذف الوسائط', deletionRequests: 'طلبات الحذف', noDeletionRequests: 'لا توجد طلبات حذف',
                deleteNow: 'حذف الآن', schedule1Day: '1 يوم', schedule1Week: '1 أسبوع', schedule1Month: '1 شهر', rejectDeletion: 'رفض', approveDeleteRequest: 'موافقة',
                alreadyRequested: 'تم طلب الحذف', deletionApproved: 'موافق عليه', deletionRejected: 'مرفوض', deletionPending: 'قيد الانتظار',
                deletionRequestSent: 'تم إرسال طلب الحذف!', deletionRequestFailed: 'فشل الإرسال', deletionActionFailed: 'فشلت المعالجة',
                deleteRequest: 'حذف الطلب', deleteMedia: 'حذف الوسائط', rejectionReasonPrompt: 'سبب الرفض (اختياري):', rejectionReasonLabel: 'السبب:',
                deletionLimitReached: 'تم الوصول لحد الطلبات', banUser: 'حظر', unbanUser: 'إلغاء الحظر', bannedUsers: 'المستخدمون المحظورون',
                ban1Day: '1 يوم', ban1Week: '1 أسبوع', ban1Month: '1 شهر', banPermanent: 'دائم', banExpires: 'ينتهي:', banPermanentLabel: 'دائم',
                bannedBy: 'بواسطة', noBannedUsers: 'لا يوجد مستخدمون محظورون', youAreBanned: 'أنت محظور من هذا الإجراء', banSuccess: 'تم حظر المستخدم', unbanSuccess: 'تم إلغاء حظر المستخدم',
                liveChat: 'الدردشة المباشرة', chatOnline: 'متصل', chatTyping: 'يكتب...', chatTypingMultiple: 'يكتبون...',
                chatSend: 'إرسال', chatPlaceholder: 'اكتب رسالة...', chatNoMessages: 'لا توجد رسائل. ابدأ المحادثة!',
                chatSearchGif: 'بحث GIF...', chatTrending: 'رائج', chatPoweredBy: 'مدعوم من Tenor',
                chatDeleted: 'تم حذف الرسالة', chatYou: 'أنت', chatJustNow: 'الآن', chatMinutesAgo: 'د', chatHoursAgo: 'س', chatYesterday: 'أمس',
                chatModerators: 'المشرفون', chatAddMod: 'إضافة مشرف', chatRemoveMod: 'إزالة', chatBannedUsers: 'المستخدمون المحظورون',
                chatBan: 'حظر', chatUnban: 'إلغاء الحظر', chatMute: 'كتم', chatBanFromMedia: 'حظر من الوسائط', chatMinutes: 'دقائق',
                chatBanned: 'أنت محظور من الدردشة', chatMuted: 'أنت مكتوم مؤقتاً', chatRateLimited: 'ببطء! رسائل كثيرة جداً.',
                chatConnecting: 'جاري الاتصال...', chatReconnecting: 'إعادة الاتصال...', chatDisconnected: 'غير متصل',
                chatClearAll: 'مسح الكل', chatClearConfirm: 'حذف جميع الرسائل؟ لا يمكن التراجع عن هذا.', chatCleared: 'تم مسح الدردشة'
            },
            hi: {
                requestMedia: 'मीडिया अनुरोध', manageRequests: 'अनुरोध प्रबंधन', requestDescription: '📬 अपनी पसंदीदा मीडिया का अनुरोध करें!',
                requestDescriptionText: 'जो फिल्में या टीवी श्रृंखला आप देखना चाहते हैं उनका अनुरोध करने के लिए इस फॉर्म का उपयोग करें।',
                mediaTitle: 'शीर्षक *', mediaTitlePlaceholder: 'उदा., ब्रेकिंग बैड, द गॉडफादर', type: 'प्रकार *', selectType: '-- प्रकार चुनें --',
                movie: 'फिल्म', tvSeries: 'टीवी श्रृंखला', anime: 'एनीमे', documentary: 'वृत्तचित्र', other: 'अन्य',
                additionalNotes: 'अतिरिक्त टिप्पणियाँ', notesPlaceholder: 'सीजन नंबर, वर्ष, विशिष्ट विवरण, आदि।', submitRequest: 'अनुरोध भेजें',
                yourRequests: 'आपके अनुरोध', loadingRequests: 'आपके अनुरोध लोड हो रहे हैं...', noRequests: 'आपने अभी तक कोई मीडिया अनुरोध नहीं किया',
                errorLoading: 'लोडिंग त्रुटि', notSpecified: 'निर्दिष्ट नहीं', noDetails: 'कोई विवरण नहीं', watchNow: '🎬 अभी देखें',
                noRequestsYet: 'अभी तक कोई अनुरोध नहीं', newRequest: 'नया अनुरोध', pending: 'लंबित', processing: 'प्रगति में', done: 'पूर्ण', rejected: 'अस्वीकृत',
                titleRequired: 'कृपया शीर्षक दर्ज करें', typeRequired: 'कृपया प्रकार चुनें', requestSubmitted: 'अनुरोध सफलतापूर्वक भेजा गया!',
                requestFailed: 'अनुरोध भेजने में विफल', statusUpdated: 'स्थिति अपडेट की गई', statusUpdateFailed: 'स्थिति अपडेट विफल',
                addLink: '+ लिंक', enterMediaLink: 'लिंक दर्ज करें:', delete: 'हटाएं', confirmDelete: 'क्या आप वाकई इस अनुरोध को हटाना चाहते हैं?',
                mediaLinkPlaceholder: 'मीडिया लिंक', unknown: 'अज्ञात', loading: 'लोड हो रहा है...',
                snooze: 'स्नूज़', unsnooze: 'अनस्नूज़', snoozed: 'स्नूज़्ड', snoozedUntil: 'तक स्नूज़्ड', snoozeDate: 'स्नूज़ तिथि',
                categoryNew: '🆕 नया', categoryProcessing: '🔄 प्रगति में', categoryPending: '⏳ लंबित', categorySnoozed: '💤 स्नूज़्ड', categoryDone: '✅ पूर्ण', categoryRejected: '❌ अस्वीकृत',
                createRequest: 'अनुरोध बनाएं', latestMedia: 'नवीनतम मीडिया', latestMediaLoading: 'लोड हो रहा है...', latestMediaEmpty: 'कोई हालिया मीडिया नहीं', latestMediaError: 'लोड विफल',
                newEpisode: '+1 एपिसोड', newEpisodes: '+{count} एपिसोड', typeMovie: 'फिल्म', typeSeries: 'श्रृंखला', typeAnime: 'एनीमे', typeOther: 'अन्य',
                timeAgo: 'पहले', timeJustNow: 'अभी', timeMinutes: 'मिनट', timeHours: 'घंटे', timeDays: 'दिन',
                mediaManagement: 'मीडिया', mediaManagementTitle: 'मीडिया प्रबंधन', mediaSearch: 'खोजें...', mediaTypeAll: 'सभी प्रकार', mediaTypeMovie: 'फिल्में', mediaTypeSeries: 'श्रृंखला',
                mediaSortBy: 'क्रमबद्ध करें', mediaSortTitle: 'शीर्षक', mediaSortYear: 'वर्ष', mediaSortRating: 'रेटिंग', mediaSortPlays: 'चलाए गए', mediaSortSize: 'आकार',
                mediaSortDateAdded: 'जोड़ने की तिथि', mediaSortPlayCount: 'चलाए गए', mediaSortWatchTime: 'देखने का समय', mediaLoading: 'लोड हो रहा है...', mediaNoResults: 'कोई मीडिया नहीं', mediaError: 'लोडिंग त्रुटि',
                mediaScheduleDelete: 'हटाने का शेड्यूल', mediaCancelDelete: 'हटाना रद्द करें', mediaDeleteIn: 'में हटाएं', mediaLeavingIn: 'में हट जाएगा',
                media1Day: '1 दिन', media3Days: '3 दिन', media1Week: '1 सप्ताह', media2Weeks: '2 सप्ताह', mediaCustom: 'कस्टम...', mediaCustomHours: 'घंटे',
                mediaSchedule: 'शेड्यूल', mediaCancel: 'रद्द करें', mediaNoScheduled: 'कोई शेड्यूल्ड हटाना नहीं', mediaScheduledBy: 'द्वारा शेड्यूल्ड', mediaDeletesIn: 'में हटेगा',
                mediaActions: 'कार्रवाई', mediaChange: 'बदलें', mediaChangeTime: 'समय बदलें', mediaSoon: 'जल्द', mediaDays: 'दिन', mediaPlays: 'बार चला',
                mediaMinutes: 'मिनट', mediaGB: 'GB', mediaMB: 'MB', mediaPage: 'पृष्ठ', mediaOf: 'का', mediaPrev: 'पिछला', mediaNext: 'अगला', mediaGo: 'जाएं',
                mediaTypeScheduled: 'शेड्यूल्ड', mediaSettings: 'सेटिंग्स', mediaIncludeTypes: 'प्रकार शामिल करें:', mediaTypesHint: 'दिखाने के लिए प्रकार चुनें',
                requestDeleteRequest: 'हटाने का अनुरोध', requestDeleteMedia: 'मीडिया हटाने का अनुरोध', deletionRequests: 'हटाने के अनुरोध', noDeletionRequests: 'कोई हटाने के अनुरोध नहीं',
                deleteNow: 'अभी हटाएं', schedule1Day: '1 दिन', schedule1Week: '1 सप्ताह', schedule1Month: '1 महीना', rejectDeletion: 'अस्वीकार', approveDeleteRequest: 'स्वीकार',
                alreadyRequested: 'हटाने का अनुरोध किया', deletionApproved: 'स्वीकृत', deletionRejected: 'अस्वीकृत', deletionPending: 'लंबित',
                deletionRequestSent: 'हटाने का अनुरोध भेजा गया!', deletionRequestFailed: 'भेजने में विफल', deletionActionFailed: 'प्रसंस्करण विफल',
                deleteRequest: 'अनुरोध हटाएं', deleteMedia: 'मीडिया हटाएं', rejectionReasonPrompt: 'अस्वीकृति का कारण (वैकल्पिक):', rejectionReasonLabel: 'कारण:',
                deletionLimitReached: 'अनुरोध सीमा पूरी हुई', banUser: 'प्रतिबंध', unbanUser: 'प्रतिबंध हटाएं', bannedUsers: 'प्रतिबंधित उपयोगकर्ता',
                ban1Day: '1 दिन', ban1Week: '1 सप्ताह', ban1Month: '1 महीना', banPermanent: 'स्थायी', banExpires: 'समाप्ति:', banPermanentLabel: 'स्थायी',
                bannedBy: 'द्वारा', noBannedUsers: 'कोई प्रतिबंधित उपयोगकर्ता नहीं', youAreBanned: 'आप इस क्रिया से प्रतिबंधित हैं', banSuccess: 'उपयोगकर्ता प्रतिबंधित', unbanSuccess: 'प्रतिबंध हटाया गया',
                liveChat: 'लाइव चैट', chatOnline: 'ऑनलाइन', chatTyping: 'टाइप कर रहा है...', chatTypingMultiple: 'टाइप कर रहे हैं...',
                chatSend: 'भेजें', chatPlaceholder: 'संदेश लिखें...', chatNoMessages: 'कोई संदेश नहीं। बातचीत शुरू करें!',
                chatSearchGif: 'GIF खोजें...', chatTrending: 'ट्रेंडिंग', chatPoweredBy: 'Tenor द्वारा संचालित',
                chatDeleted: 'संदेश हटाया गया', chatYou: 'आप', chatJustNow: 'अभी', chatMinutesAgo: 'मिनट', chatHoursAgo: 'घंटे', chatYesterday: 'कल',
                chatModerators: 'मॉडरेटर', chatAddMod: 'मॉडरेटर जोड़ें', chatRemoveMod: 'हटाएं', chatBannedUsers: 'प्रतिबंधित उपयोगकर्ता',
                chatBan: 'प्रतिबंध', chatUnban: 'प्रतिबंध हटाएं', chatMute: 'म्यूट', chatBanFromMedia: 'मीडिया से प्रतिबंध', chatMinutes: 'मिनट',
                chatBanned: 'आप चैट से प्रतिबंधित हैं', chatMuted: 'आप अस्थायी रूप से म्यूट हैं', chatRateLimited: 'धीरे! बहुत सारे संदेश।',
                chatConnecting: 'कनेक्ट हो रहा है...', chatReconnecting: 'पुनः कनेक्ट हो रहा है...', chatDisconnected: 'डिस्कनेक्ट',
                chatClearAll: 'सब साफ करें', chatClearConfirm: 'सभी संदेश हटाएं? यह पूर्ववत नहीं किया जा सकता।', chatCleared: 'चैट साफ़ हो गई'
            },
            lt: {
                requestMedia: 'Užsakyti Mediją', manageRequests: 'Tvarkyti Medijos Užklausas', requestDescription: '📬 Užsakykite Savo Mėgstamą Mediją!',
                requestDescriptionText: 'Naudokite šią formą, kad užsakytumėte filmus ar TV serialus, kuriuos norėtumėte žiūrėti.',
                mediaTitle: 'Medijos Pavadinimas *', mediaTitlePlaceholder: 'pvz., Breaking Bad, Krikštatėvis', type: 'Tipas *', selectType: '-- Pasirinkite Tipą --',
                movie: 'Filmas', tvSeries: 'TV Serialas', anime: 'Anime', documentary: 'Dokumentika', other: 'Kita',
                additionalNotes: 'Papildomos Pastabos', notesPlaceholder: 'Sezono numeris, metai, specifinė informacija ir t.t.', submitRequest: 'Pateikti Užklausą',
                yourRequests: 'Jūsų Užklausos', loadingRequests: 'Kraunamos jūsų užklausos...', noRequests: 'Jūs dar neužsakėte jokios medijos',
                errorLoading: 'Klaida kraunant jūsų užklausas', notSpecified: 'Nenurodyta', noDetails: 'Nėra detalių', watchNow: '🎬 Žiūrėti Dabar',
                noRequestsYet: 'Medijos užklausų dar nėra', newRequest: 'Nauja užklausa', pending: 'LAUKIAMA', processing: 'VYKDOMA', done: 'ATLIKTA', rejected: 'ATMESTA',
                titleRequired: 'Įveskite medijos pavadinimą', typeRequired: 'Pasirinkite medijos tipą', requestSubmitted: 'Užklausa sėkmingai pateikta!',
                requestFailed: 'Nepavyko pateikti užklausos', statusUpdated: 'Būsena atnaujinta', statusUpdateFailed: 'Nepavyko atnaujinti būsenos',
                addLink: '+ Nuoroda', enterMediaLink: 'Įveskite medijos nuorodą:', delete: 'Ištrinti', confirmDelete: 'Ar tikrai norite ištrinti šią užklausą?',
                mediaLinkPlaceholder: 'Medijos nuoroda', unknown: 'Nežinoma', loading: 'Kraunama...',
                snooze: 'Atidėti', unsnooze: 'Atšaukti atidėjimą', snoozed: 'ATIDĖTA', snoozedUntil: 'Atidėta iki', snoozeDate: 'Atidėti iki datos',
                categoryNew: '🆕 Nauji', categoryProcessing: '🔄 Vykdoma', categoryPending: '⏳ Laukiama', categorySnoozed: '💤 Atidėta', categoryDone: '✅ Atlikta', categoryRejected: '❌ Atmesta',
                createRequest: 'Sukurti Užklausą', latestMedia: 'Naujausia Medija', latestMediaLoading: 'Kraunama...', latestMediaEmpty: 'Naujų medijų nerasta', latestMediaError: 'Nepavyko įkelti',
                newEpisode: '+1 serija', newEpisodes: '+{count} serijos', typeMovie: 'Filmas', typeSeries: 'Serialas', typeAnime: 'Anime', typeOther: 'Kita',
                timeAgo: 'prieš', timeJustNow: 'ką tik', timeMinutes: 'min', timeHours: 'val', timeDays: 'd',
                mediaManagement: 'Medija', mediaManagementTitle: 'Medijos Valdymas', mediaSearch: 'Ieškoti...', mediaTypeAll: 'Visi Tipai', mediaTypeMovie: 'Filmai', mediaTypeSeries: 'Serialai',
                mediaSortBy: 'Rūšiuoti pagal', mediaSortTitle: 'Pavadinimas', mediaSortYear: 'Metai', mediaSortRating: 'Reitingas', mediaSortPlays: 'Peržiūros', mediaSortSize: 'Dydis',
                mediaSortDateAdded: 'Pridėjimo data', mediaSortPlayCount: 'Peržiūros', mediaSortWatchTime: 'Žiūrėjimo laikas', mediaLoading: 'Kraunama...', mediaNoResults: 'Medija nerasta', mediaError: 'Klaida kraunant',
                mediaScheduleDelete: 'Planuoti Ištrynimą', mediaCancelDelete: 'Atšaukti Ištrynimą', mediaDeleteIn: 'Ištrinti po', mediaLeavingIn: 'Išeina po',
                media1Day: '1 Diena', media3Days: '3 Dienos', media1Week: '1 Savaitė', media2Weeks: '2 Savaitės', mediaCustom: 'Pasirinkti...', mediaCustomHours: 'Valandos',
                mediaSchedule: 'Planuoti', mediaCancel: 'Atšaukti', mediaNoScheduled: 'Nėra suplanuotų ištrynimų', mediaScheduledBy: 'Suplanavo', mediaDeletesIn: 'Ištrins po',
                mediaActions: 'Veiksmai', mediaChange: 'Keisti', mediaChangeTime: 'Keisti laiką', mediaSoon: 'Greitai', mediaDays: 'dienų', mediaPlays: 'peržiūrų',
                mediaMinutes: 'min', mediaGB: 'GB', mediaMB: 'MB', mediaPage: 'Puslapis', mediaOf: 'iš', mediaPrev: 'Ankstesnis', mediaNext: 'Kitas', mediaGo: 'Eiti',
                mediaTypeScheduled: 'Suplanuoti', mediaSettings: 'Nustatymai', mediaIncludeTypes: 'Rodyti tipus:', mediaTypesHint: 'Pasirinkite rodytinus tipus',
                requestDeleteRequest: 'Prašyti ištrinti', requestDeleteMedia: 'Prašyti ištrinti mediją', deletionRequests: 'Ištrynimo Užklausos', noDeletionRequests: 'Ištrynimo užklausų nėra',
                deleteNow: 'Ištrinti ~1val', schedule1Day: '1 Diena', schedule1Week: '1 Savaitė', schedule1Month: '1 Mėnuo', rejectDeletion: 'Atmesti', approveDeleteRequest: 'Patvirtinti',
                alreadyRequested: 'Ištrynimas Užsakytas', deletionApproved: 'PATVIRTINTA', deletionRejected: 'ATMESTA', deletionPending: 'LAUKIAMA',
                deletionRequestSent: 'Ištrynimo užklausa išsiųsta!', deletionRequestFailed: 'Nepavyko išsiųsti', deletionActionFailed: 'Nepavyko apdoroti',
                deleteRequest: 'Ištrinti Užklausą', deleteMedia: 'Ištrinti Mediją', rejectionReasonPrompt: 'Atmetimo priežastis (neprivaloma):', rejectionReasonLabel: 'Priežastis:',
                deletionLimitReached: 'Pasiektas užklausų limitas', banUser: 'Uždrausti', unbanUser: 'Atblokuoti', bannedUsers: 'Uždrausti Vartotojai',
                ban1Day: '1 Diena', ban1Week: '1 Savaitė', ban1Month: '1 Mėnuo', banPermanent: 'Visam laikui', banExpires: 'Baigiasi:', banPermanentLabel: 'Visam laikui',
                bannedBy: 'uždraudė', noBannedUsers: 'Nėra uždraustų vartotojų', youAreBanned: 'Jums uždrausta atlikti šį veiksmą', banSuccess: 'Vartotojas uždraustas', unbanSuccess: 'Vartotojas atblokuotas',
                liveChat: 'Pokalbiai', chatOnline: 'prisijungę', chatTyping: 'rašo...', chatTypingMultiple: 'rašo...',
                chatSend: 'Siųsti', chatPlaceholder: 'Rašykite žinutę...', chatNoMessages: 'Žinučių nėra. Pradėkite pokalbį!',
                chatSearchGif: 'Ieškoti GIF...', chatTrending: 'Populiarūs', chatPoweredBy: 'Veikia su Tenor',
                chatDeleted: 'Žinutė ištrinta', chatYou: 'Jūs', chatJustNow: 'Ką tik', chatMinutesAgo: 'min', chatHoursAgo: 'val', chatYesterday: 'Vakar',
                chatModerators: 'Moderatoriai', chatAddMod: 'Pridėti Moderatorių', chatRemoveMod: 'Pašalinti', chatBannedUsers: 'Uždrausti Vartotojai',
                chatBan: 'Uždrausti', chatUnban: 'Atblokuoti', chatMute: 'Nutildyti', chatBanFromMedia: 'Uždrausti Mediją', chatMinutes: 'minučių',
                chatBanned: 'Jums uždrausta rašyti', chatMuted: 'Jūs laikinai nutildytas', chatRateLimited: 'Lėčiau! Per daug žinučių.',
                chatConnecting: 'Jungiamasi...', chatReconnecting: 'Jungiantis iš naujo...', chatDisconnected: 'Atsijungta',
                chatClearAll: 'Išvalyti viską', chatClearConfirm: 'Ištrinti visas žinutes? Šio veiksmo negalima atšaukti.', chatCleared: 'Pokalbis išvalytas'
            }
        },

        // Get translation for current language
        t: function(key) {
            return this.translations[this.currentLanguage]?.[key] || this.translations.en[key] || key;
        },

        // Set language and refresh modal if open
        setLanguage: function(lang) {
            if (!this.validLanguages.includes(lang)) lang = 'en';
            this.currentLanguage = lang;
            localStorage.setItem('ratingsPluginLanguage', lang);
            // Update language selector visual state
            const selectors = document.querySelectorAll('.language-selector');
            selectors.forEach(function(sel) { sel.value = lang; });
            // Update button text
            const btnText = document.querySelector('#requestMediaBtn .btn-text');
            if (btnText) {
                btnText.textContent = this.t('requestMedia');
            }
        },

        // Generate language selector dropdown HTML
        getLanguageSelectorHtml: function(id) {
            var self = this;
            var langNames = {
                en: 'English', es: 'Español', zh: '中文', pt: 'Português', ru: 'Русский',
                ja: '日本語', de: 'Deutsch', fr: 'Français', ko: '한국어', it: 'Italiano',
                tr: 'Türkçe', pl: 'Polski', nl: 'Nederlands', ar: 'العربية', hi: 'हिन्दी', lt: 'Lietuvių'
            };
            var options = this.validLanguages.map(function(code) {
                var selected = code === self.currentLanguage ? ' selected' : '';
                return '<option value="' + code + '"' + selected + '>' + langNames[code] + '</option>';
            }).join('');
            return '<select id="' + id + '" class="language-selector" style="padding:4px 8px;border-radius:4px;border:1px solid rgba(255,255,255,0.2);background:#1a1a1a;color:#fff;font-size:12px;cursor:pointer;">' + options + '</select>';
        },

        /**
         * Load badge display profiles from server config
         */
        loadBadgeDisplayProfiles: function () {
            if (!this.ratingsEnabled) return;
            const self = this;
            if (!window.ApiClient) return;

            const baseUrl = ApiClient.serverAddress();
            fetch(baseUrl + '/Ratings/Config', { method: 'GET', credentials: 'include' })
                .then(function (response) { return response.json(); })
                .then(function (config) {
                    if (config.BadgeDisplayProfiles) {
                        try {
                            var raw = JSON.parse(config.BadgeDisplayProfiles);
                            // Migrate old minWidth/maxWidth format to new minValue/maxValue + axis
                            self.badgeDisplayProfiles = raw.map(function (p) {
                                if (p.minWidth !== undefined && p.minValue === undefined) {
                                    p.minValue = p.minWidth;
                                }
                                if (p.maxWidth !== undefined && p.maxValue === undefined) {
                                    p.maxValue = p.maxWidth;
                                }
                                if (!p.axis) p.axis = 'horizontal';
                                return p;
                            });
                        } catch (e) {
                            self.badgeDisplayProfiles = [];
                        }
                    } else {
                        self.badgeDisplayProfiles = [];
                    }
                    // Apply to any existing rating widget
                    self.applyBadgeProfile();
                })
                .catch(function () {
                    self.badgeDisplayProfiles = [];
                });
        },

        /**
         * Find matching badge profile for current screen dimensions.
         * Supports axis: 'horizontal' (matches window.innerWidth) and 'vertical' (matches window.innerHeight).
         * Backward compat: old profiles with minWidth/maxWidth treated as horizontal.
         */
        getMatchingBadgeProfile: function () {
            var screenWidth = window.innerWidth;
            var screenHeight = window.innerHeight;
            var bestMatch = null;
            var bestRange = Infinity;

            for (var i = 0; i < this.badgeDisplayProfiles.length; i++) {
                var profile = this.badgeDisplayProfiles[i];
                var axis = profile.axis || 'horizontal';

                // Determine which dimension and range fields to use
                var dimension, minVal, maxVal;
                if (axis === 'vertical') {
                    dimension = screenHeight;
                    minVal = profile.minValue !== undefined ? profile.minValue : 0;
                    maxVal = profile.maxValue !== undefined ? profile.maxValue : 9999;
                } else {
                    dimension = screenWidth;
                    // Backward compat: old minWidth/maxWidth fields
                    minVal = profile.minValue !== undefined ? profile.minValue : (profile.minWidth || 0);
                    maxVal = profile.maxValue !== undefined ? profile.maxValue : (profile.maxWidth || 9999);
                }

                if (dimension >= minVal && dimension <= maxVal) {
                    var range = maxVal - minVal;
                    if (range < bestRange) {
                        bestMatch = profile;
                        bestRange = range;
                    }
                }
            }

            return bestMatch;
        },

        /**
         * Apply badge display profile to the rating widget.
         * Uses inline styles to override the hardcoded @media CSS defaults.
         */
        applyBadgeProfile: function () {
            var container = document.getElementById('ratingsPluginComponent');
            if (!container) return;

            var profile = this.getMatchingBadgeProfile();
            var statsEl = container.querySelector('.ratings-plugin-stats');

            if (!profile) {
                // No matching profile - remove all inline overrides, let default CSS apply
                container.classList.remove('badge-profile-active');
                container.style.removeProperty('transform');
                container.style.removeProperty('background');
                container.style.removeProperty('margin');
                container.style.removeProperty('padding');
                if (statsEl) {
                    statsEl.style.removeProperty('display');
                }
                return;
            }

            // Mark as profile-active
            container.classList.add('badge-profile-active');

            // Build inline styles that override @media defaults
            var translateX = (profile.offsetX || 0) + 'vw';
            var translateY = (profile.offsetY || 0) + 'vh';
            var scale = 1 + ((profile.sizePercent || 0) / 100);

            // Use cssText to set all overrides at once with !important to beat @media rules
            var css = 'transform: translate(' + translateX + ', ' + translateY + ') scale(' + scale + ') !important;';
            css += ' margin: 0 !important;';
            css += ' padding: 0 !important;';

            if (profile.removeBackground) {
                css += ' background: transparent !important;';
            }

            container.style.cssText = css;

            // Hide text (show stars only)
            if (statsEl) {
                if (profile.hideText) {
                    statsEl.style.display = 'none';
                } else {
                    statsEl.style.removeProperty('display');
                }
            }
        },

        /**
         * Initialize the ratings plugin
         */
        init: function () {
            var self = this;

            // Load saved language preference or fetch from server config
            const savedLang = localStorage.getItem('ratingsPluginLanguage');
            if (savedLang && this.validLanguages.includes(savedLang)) {
                this.currentLanguage = savedLang;
            } else if (window.ApiClient) {
                // No valid saved preference, try to load default from server
                const baseUrl = ApiClient.serverAddress();
                fetch(baseUrl + '/Ratings/Config', { method: 'GET', credentials: 'include' })
                    .then(function (response) { return response.json(); })
                    .then(function (config) {
                        if (config.DefaultLanguage && self.validLanguages.includes(config.DefaultLanguage)) {
                            self.currentLanguage = config.DefaultLanguage;
                        }
                    })
                    .catch(function () { /* Use default 'en' */ });
            }

            this.injectStyles();
            this.observeDetailPages();
            this.observeHomePageCards();

            // Initialize request button with multiple attempts for reliability
            this.initRequestButtonWithRetry();

            // Initialize search field in header
            this.initSearchField();

            // Initialize notification toggle in header
            this.initNotificationToggle();

            // Initialize latest media button (replaces sync play)
            this.initLatestMediaButton();

            // Initialize responsive scaling
            this.updateResponsiveScaling();

            // Initialize Netflix view if enabled
            this.initNetflixView();

            // Initialize new media notifications
            this.initNotifications();

            // Initialize media management button (admin only)
            this.initMediaManagementButtonWithRetry();

            // Initialize deletion badges (for all users)
            this.initDeletionBadges();

            // Load badge display profiles and listen for resize
            this.loadBadgeDisplayProfiles();
            window.addEventListener('resize', function () {
                self.applyBadgeProfile();
            });

            // Load EnableRatings flag from config (with retry for ApiClient)
            this.loadRatingsEnabledFlag();

            // Initialize chat feature
            this.initChatWithRetry();
        },

        /**
         * Load the EnableRatings flag from server config with retry.
         * Sets ratingsEnabled to false if disabled, which gates widget injection and card ratings.
         */
        loadRatingsEnabledFlag: function () {
            const self = this;
            var attempts = 0;

            var tryLoad = function () {
                attempts++;
                if (!window.ApiClient) {
                    if (attempts < 15) {
                        setTimeout(tryLoad, 1000);
                    }
                    return;
                }
                var baseUrl = ApiClient.serverAddress();
                fetch(baseUrl + '/Ratings/Config', { method: 'GET', credentials: 'include' })
                    .then(function (r) { return r.json(); })
                    .then(function (config) {
                        self.ratingsEnabled = config.EnableRatings !== false;
                    })
                    .catch(function () {
                        // Default to enabled on error
                        self.ratingsEnabled = true;
                    });
            };

            setTimeout(tryLoad, 500);
        },

        /**
         * Initialize request button with retry logic for SPA navigation
         */
        initRequestButtonWithRetry: function () {
            const self = this;
            let attempts = 0;
            const maxAttempts = 10;

            const tryInit = () => {
                attempts++;
                try {
                    // Check if button already exists
                    if (document.getElementById('requestMediaBtn')) {
                        return; // Already initialized
                    }

                    // Check if ApiClient is ready
                    if (!window.ApiClient) {
                        if (attempts < maxAttempts) {
                            setTimeout(tryInit, 1000);
                        }
                        return;
                    }

                    // Check if request button is enabled in config
                    const baseUrl = ApiClient.serverAddress();
                    fetch(`${baseUrl}/Ratings/Config`, { method: 'GET', credentials: 'include' })
                        .then(response => response.json())
                        .then(config => {
                            if (config.EnableRequestButton === true) {
                                self.initRequestButton();
                            }
                        })
                        .catch(() => {
                            // Default to showing button if config fails
                            self.initRequestButton();
                        });
                } catch (err) {
                    console.error('Request button init attempt failed:', err);
                    if (attempts < maxAttempts) {
                        setTimeout(tryInit, 1000);
                    }
                }
            };

            // Initial attempt after short delay
            setTimeout(tryInit, 1500);

            // Also try on page visibility change (when user returns to tab)
            document.addEventListener('visibilitychange', () => {
                if (document.visibilityState === 'visible' && !document.getElementById('requestMediaBtn')) {
                    setTimeout(tryInit, 500);
                }
            });

            // Listen for Jellyfin navigation events if available
            try {
                if (window.Emby && window.Emby.Page && typeof Emby.Page.addEventListener === 'function') {
                    Emby.Page.addEventListener('pageshow', () => {
                        if (!document.getElementById('requestMediaBtn')) {
                            setTimeout(tryInit, 500);
                        }
                    });
                }
            } catch (e) {
                // Emby.Page.addEventListener not available in this version
            }
        },

        /**
         * Inject CSS styles for the rating component
         */
        injectStyles: function () {
            if (document.getElementById('ratingsPluginStyles')) {
                return;
            }

            const styles = `
                .ratings-plugin-ribbon-anchor {
                    position: relative;
                }

                .ratings-plugin-container {
                    position: absolute;
                    bottom: 100%;
                    left: 0;
                    z-index: 100;
                    background: rgba(0, 0, 0, 0.6);
                    border-radius: 6px;
                    padding: 0.3em 0.6em;
                    display: flex;
                    flex-direction: column;
                    align-items: center;
                    gap: 0.15em;
                    pointer-events: auto;
                }

                .ratings-plugin-star {
                    font-size: 1.6em;
                }

                @media (min-width: 1200px) {
                    .ratings-plugin-star {
                        font-size: 1.8em;
                    }
                }

                @media (max-width: 1024px) {
                    .ratings-plugin-container {
                        left: 55% !important;
                        transform: translateX(-50%);
                    }
                }
                @media (max-width: 768px) {
                    .ratings-plugin-star {
                        font-size: 1.3em;
                    }
                }

                @media (max-width: 480px) {
                    .ratings-plugin-star {
                        font-size: 1.1em;
                    }
                    .ratings-plugin-stats {
                        font-size: 0.8em;
                    }
                    .ratings-plugin-stars {
                        gap: 0.1em;
                    }
                    .ratings-plugin-container {
                        padding: 0.2em 0.4em;
                        gap: 0.1em;
                    }
                }

                .ratings-plugin-title {
                    font-size: 1.2em;
                    font-weight: 500;
                    margin-bottom: 0.5em;
                    color: #fff;
                }

                .ratings-plugin-stars {
                    display: flex;
                    flex-wrap: wrap;
                    justify-content: center;
                    align-items: center;
                    gap: 0.25em;
                    position: relative;
                }

                .ratings-plugin-star {
                    cursor: pointer;
                    color: #555;
                    transition: all 0.2s ease;
                    user-select: none;
                }

                .ratings-plugin-star:hover,
                .ratings-plugin-star.hover {
                    color: #ffd700;
                    transform: scale(1.1);
                }

                .ratings-plugin-star.filled {
                    color: #ffd700;
                }

                .ratings-plugin-stats {
                    font-size: 0.9em;
                    color: #bbb;
                    text-align: center;
                }

                .ratings-plugin-average {
                    font-size: 1.1em;
                    font-weight: 600;
                    color: #ffd700;
                    margin-left: 0.5em;
                }

                .ratings-plugin-popup {
                    position: absolute;
                    bottom: 100%;
                    left: 0;
                    background: rgba(20, 20, 20, 0.98);
                    border: 1px solid #444;
                    border-radius: 8px;
                    padding: 1em;
                    min-width: 250px;
                    max-width: 400px;
                    max-height: 400px;
                    overflow-y: auto;
                    box-shadow: 0 4px 20px rgba(0, 0, 0, 0.5);
                    z-index: 10000;
                    margin-bottom: 0.5em;
                    display: none;
                }

                .ratings-plugin-popup.visible {
                    display: block;
                }

                .ratings-plugin-popup-title {
                    font-size: 1em;
                    font-weight: 600;
                    margin-bottom: 0.8em;
                    color: #fff;
                    border-bottom: 1px solid #444;
                    padding-bottom: 0.5em;
                }

                .ratings-plugin-popup-list {
                    list-style: none;
                    padding: 0;
                    margin: 0;
                }

                .ratings-plugin-popup-item {
                    display: flex;
                    justify-content: space-between;
                    align-items: center;
                    padding: 0.5em 0;
                    border-bottom: 1px solid #333;
                }

                .ratings-plugin-popup-item:last-child {
                    border-bottom: none;
                }

                .ratings-plugin-popup-username {
                    color: #fff;
                    font-weight: 500;
                    flex: 1;
                }

                .ratings-plugin-popup-rating {
                    color: #ffd700;
                    font-weight: 600;
                    font-size: 1.1em;
                    margin-left: 1em;
                }

                .ratings-plugin-popup-empty {
                    color: #999;
                    text-align: center;
                    padding: 1em;
                    font-style: italic;
                }

                .ratings-plugin-your-rating {
                    font-size: 0.85em;
                    color: #4CAF50;
                    margin-top: 0.3em;
                }

                .ratings-plugin-loading {
                    color: #999;
                    font-style: italic;
                }

                /* Card overlay ratings */
                .cardImageContainer.has-rating::after,
                .cardContent.has-rating::after,
                .card-imageContainer.has-rating::after {
                    content: attr(data-rating);
                    position: absolute;
                    top: 5px;
                    left: 5px;
                    background: rgba(0, 0, 0, 0.85);
                    color: #fff;
                    padding: 4px 8px;
                    border-radius: 4px;
                    font-size: 0.85em;
                    z-index: 1000;
                    pointer-events: none;
                    font-weight: 600;
                }

                .ratings-plugin-card-star {
                    color: #ffd700;
                    font-size: 1em;
                }

                .ratings-plugin-card-rating {
                    color: #fff;
                    font-weight: 600;
                }

                /* Request Media Button - Aligned with Header */
                #requestMediaBtn {
                    position: absolute !important;
                    top: 8px;
                    right: 240px !important;
                    background: rgba(60, 60, 60, 0.9) !important;
                    border: 1px solid rgba(255, 255, 255, 0.2) !important;
                    padding: 12px 48px !important;
                    border-radius: 25px !important;
                    font-size: 16px !important;
                    font-weight: 600 !important;
                    cursor: pointer !important;
                    z-index: 999999 !important;
                    transition: transform 0.3s ease, background 0.3s ease, border-color 0.3s ease !important;
                    font-family: "Poppins", -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif !important;
                    -webkit-animation: pulseButton 2s ease-in-out infinite !important;
                    -moz-animation: pulseButton 2s ease-in-out infinite !important;
                    -o-animation: pulseButton 2s ease-in-out infinite !important;
                    animation: pulseButton 2s ease-in-out infinite !important;
                }

                #requestMediaBtn .btn-text {
                    background: linear-gradient(to right, #9f9f9f 0%, #fff 10%, #868686 20%) !important;
                    background-size: 200% auto !important;
                    -webkit-background-clip: text !important;
                    -webkit-text-fill-color: transparent !important;
                    background-clip: text !important;
                    -webkit-text-size-adjust: none !important;
                    display: inline-block !important;
                    -webkit-animation: shine 3s linear infinite !important;
                    -moz-animation: shine 3s linear infinite !important;
                    -o-animation: shine 3s linear infinite !important;
                    animation: shine 3s linear infinite !important;
                }

                @keyframes shine {
                    0% {
                        background-position: 0;
                    }
                    60% {
                        background-position: 180px;
                    }
                    100% {
                        background-position: 180px;
                    }
                }

                @-webkit-keyframes shine {
                    0% {
                        background-position: 0;
                    }
                    60% {
                        background-position: 180px;
                    }
                    100% {
                        background-position: 180px;
                    }
                }

                @-moz-keyframes shine {
                    0% {
                        background-position: 0;
                    }
                    60% {
                        background-position: 180px;
                    }
                    100% {
                        background-position: 180px;
                    }
                }

                @-o-keyframes shine {
                    0% {
                        background-position: 0;
                    }
                    60% {
                        background-position: 180px;
                    }
                    100% {
                        background-position: 180px;
                    }
                }

                @keyframes pulseButton {
                    0%, 100% {
                        box-shadow: 0 4px 15px rgba(0, 0, 0, 0.5), 0 0 0 0 rgba(102, 126, 234, 0.7);
                    }
                    50% {
                        box-shadow: 0 4px 15px rgba(0, 0, 0, 0.5), 0 0 0 8px rgba(102, 126, 234, 0);
                    }
                }

                #requestMediaBtn:hover {
                    background: rgba(70, 70, 70, 0.95) !important;
                    border-color: rgba(255, 255, 255, 0.3) !important;
                    transform: scale(1.05) !important;
                }

                #requestMediaBtn.hidden {
                    display: none !important;
                }

                /* Mobile Responsive - Dynamic scaling handled by JavaScript */
                @media screen and (max-width: 925px) {
                    #requestMediaBtn {
                        padding: 8px 16px !important;
                        font-size: 16px !important;
                        border-radius: 55px !important;
                        right: 6px !important;
                    }

                    #requestMediaBtn .btn-text {
                        font-size: 16px !important;
                    }

                    .request-badge {
                        width: 16px !important;
                        height: 16px !important;
                        font-size: 9px !important;
                        top: -5px !important;
                        right: -5px !important;
                    }
                }

                /* Notification Badge */
                .request-badge {
                    position: absolute !important;
                    top: -8px !important;
                    right: -8px !important;
                    background: #ff4444 !important;
                    color: white !important;
                    border-radius: 50% !important;
                    width: 22px !important;
                    height: 22px !important;
                    display: flex !important;
                    align-items: center !important;
                    justify-content: center !important;
                    font-size: 11px !important;
                    font-weight: 700 !important;
                    border: 2px solid #1e1e1e !important;
                    animation: badgePulse 1.5s ease-in-out infinite !important;
                }

                @keyframes badgePulse {
                    0%, 100% {
                        transform: scale(1);
                    }
                    50% {
                        transform: scale(1.1);
                    }
                }

                /* Button Tooltip */
                #requestMediaBtn::after {
                    content: attr(data-tooltip) !important;
                    position: absolute !important;
                    bottom: -45px !important;
                    left: 50% !important;
                    transform: translateX(-50%) !important;
                    background: rgba(0, 0, 0, 0.95) !important;
                    color: #fff !important;
                    padding: 8px 12px !important;
                    border-radius: 6px !important;
                    font-size: 12px !important;
                    white-space: nowrap !important;
                    opacity: 0 !important;
                    pointer-events: none !important;
                    transition: opacity 0.3s ease !important;
                    z-index: 10000000 !important;
                }

                #requestMediaBtn:hover::after {
                    opacity: 1 !important;
                }

                /* Search Field in Header */
                #headerSearchField {
                    position: absolute !important;
                    top: 8px;
                    right: 480px !important;
                    z-index: 999998 !important;
                    display: flex !important;
                    align-items: center !important;
                    background: rgba(60, 60, 60, 0.9) !important;
                    border: 1px solid rgba(255, 255, 255, 0.2) !important;
                    border-radius: 25px !important;
                    padding: 8px 16px !important;
                    transition: all 0.3s ease !important;
                }

                #headerSearchField:hover {
                    background: rgba(70, 70, 70, 0.95) !important;
                    border-color: rgba(255, 255, 255, 0.4) !important;
                }

                #headerSearchField.hidden {
                    display: none !important;
                }

                #headerSearchIcon {
                    font-size: 18px !important;
                    margin-right: 8px !important;
                    cursor: pointer !important;
                    opacity: 0.8 !important;
                    transition: opacity 0.3s ease !important;
                }

                #headerSearchIcon:hover {
                    opacity: 1 !important;
                }

                #headerSearchInput {
                    background: transparent !important;
                    background-color: transparent !important;
                    border: none !important;
                    outline: none !important;
                    color: #fff !important;
                    font-size: 14px !important;
                    font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif !important;
                    width: 200px !important;
                    padding: 4px 0 !important;
                    -webkit-appearance: none !important;
                    -moz-appearance: none !important;
                    appearance: none !important;
                }

                #headerSearchInput:focus {
                    background: transparent !important;
                    background-color: transparent !important;
                    outline: none !important;
                }

                #headerSearchInput:-webkit-autofill,
                #headerSearchInput:-webkit-autofill:hover,
                #headerSearchInput:-webkit-autofill:focus,
                #headerSearchInput:-webkit-autofill:active {
                    -webkit-box-shadow: 0 0 0 30px rgba(60, 60, 60, 0.9) inset !important;
                    -webkit-text-fill-color: #fff !important;
                    background-color: transparent !important;
                    transition: background-color 5000s ease-in-out 0s !important;
                }

                #headerSearchInput::placeholder {
                    color: rgba(255, 255, 255, 0.5) !important;
                }

                /* Mobile Responsive for Search Field - Dynamic scaling handled by JavaScript */
                @media screen and (max-width: 925px) {
                    #headerSearchField {
                        left: 6px !important;
                        right: auto !important;
                        padding: 8px 16px !important;
                    }

                    #headerSearchInput {
                        width: 100px !important;
                        font-size: 14px !important;
                    }

                    #headerSearchIcon {
                        font-size: 18px !important;
                        margin-right: 8px !important;
                    }
                }

                /* Search Dropdown Results */
                #searchDropdown {
                    position: fixed !important;
                    min-width: 350px !important;
                    max-width: 450px !important;
                    max-height: 70vh !important;
                    overflow-y: auto !important;
                    background: #1e1e1e !important;
                    border: 1px solid rgba(255, 255, 255, 0.3) !important;
                    border-radius: 12px !important;
                    box-shadow: 0 8px 32px rgba(0, 0, 0, 0.8) !important;
                    z-index: 2147483647 !important;
                    display: none;
                }

                #searchDropdown.visible {
                    display: block !important;
                }

                #searchDropdown .dropdown-loading {
                    padding: 20px !important;
                    text-align: center !important;
                    color: #999 !important;
                    font-size: 13px !important;
                }

                #searchDropdown .dropdown-empty {
                    padding: 20px !important;
                    text-align: center !important;
                    color: #888 !important;
                    font-size: 13px !important;
                }

                #searchDropdown .dropdown-item {
                    display: flex !important;
                    align-items: center !important;
                    padding: 10px 12px !important;
                    cursor: pointer !important;
                    transition: background 0.2s ease !important;
                    border-bottom: 1px solid rgba(255, 255, 255, 0.05) !important;
                    text-decoration: none !important;
                }

                #searchDropdown .dropdown-item:hover {
                    background: rgba(255, 255, 255, 0.1) !important;
                }

                #searchDropdown .dropdown-item:last-child {
                    border-bottom: none !important;
                }

                #searchDropdown .dropdown-item-image {
                    width: 45px !important;
                    height: 65px !important;
                    object-fit: cover !important;
                    border-radius: 4px !important;
                    margin-right: 12px !important;
                    background: #333 !important;
                    flex-shrink: 0 !important;
                }

                #searchDropdown .dropdown-item-info {
                    flex: 1 !important;
                    min-width: 0 !important;
                    overflow: hidden !important;
                }

                #searchDropdown .dropdown-item-title {
                    color: #fff !important;
                    font-size: 14px !important;
                    font-weight: 500 !important;
                    white-space: nowrap !important;
                    overflow: hidden !important;
                    text-overflow: ellipsis !important;
                    margin-bottom: 4px !important;
                }

                #searchDropdown .dropdown-item-meta {
                    color: #888 !important;
                    font-size: 11px !important;
                    display: flex !important;
                    gap: 8px !important;
                }

                #searchDropdown .dropdown-item-type {
                    background: rgba(0, 164, 220, 0.3) !important;
                    color: #00a4dc !important;
                    padding: 2px 6px !important;
                    border-radius: 4px !important;
                    font-size: 10px !important;
                    font-weight: 600 !important;
                }

                #searchDropdown .dropdown-item-year {
                    color: #666 !important;
                }

                @media screen and (max-width: 925px) {
                    #searchDropdown {
                        min-width: 280px !important;
                        max-width: 320px !important;
                        left: 0 !important;
                        right: auto !important;
                    }

                    #searchDropdown .dropdown-item-image {
                        width: 40px !important;
                        height: 58px !important;
                    }

                    #searchDropdown .dropdown-item-title {
                        font-size: 13px !important;
                    }
                }

                /* Notification Toggle Styles - Positioned LEFT of search field */
                /* Search field: right:480px, ~258px wide = ends at ~738px from right */
                /* Toggle must be at right:750px+ to be LEFT of search */
                #notificationToggle {
                    position: absolute !important;
                    top: 8px;
                    right: 755px !important;
                    z-index: 999998 !important;
                    display: flex !important;
                    align-items: center !important;
                    justify-content: center !important;
                    background: transparent !important;
                    border: none !important;
                    padding: 4px !important;
                    cursor: pointer !important;
                    overflow: visible !important;
                    transition: opacity 0.2s ease !important;
                }

                #notificationToggle:hover {
                    opacity: 0.7 !important;
                }

                #notificationToggle.hidden {
                    display: none !important;
                }

                #notificationToggleIcon {
                    font-size: 25px !important;
                    opacity: 0.8 !important;
                    position: relative !important;
                }

                /* Red cross lines when notifications disabled */
                #notificationToggle.disabled::before,
                #notificationToggle.disabled::after {
                    content: '' !important;
                    position: absolute !important;
                    top: 50% !important;
                    left: 50% !important;
                    width: 3px !important;
                    height: 34px !important;
                    background: #ff1744 !important;
                    border-radius: 2px !important;
                    pointer-events: none !important;
                    box-shadow: 0 0 4px rgba(255, 23, 68, 0.6) !important;
                }

                #notificationToggle.disabled::before {
                    transform: translate(-50%, -50%) rotate(45deg) !important;
                }

                #notificationToggle.disabled::after {
                    transform: translate(-50%, -50%) rotate(-45deg) !important;
                }

                /* Tooltip for notification toggle - using fixed positioning to avoid clipping */
                #notificationTooltip {
                    position: fixed !important;
                    background: rgba(20, 20, 20, 0.98) !important;
                    color: #fff !important;
                    padding: 10px 14px !important;
                    border-radius: 8px !important;
                    font-size: 13px !important;
                    white-space: nowrap !important;
                    opacity: 0;
                    visibility: hidden;
                    transition: opacity 0.2s ease, visibility 0.2s ease !important;
                    pointer-events: none !important;
                    z-index: 99999999 !important;
                    box-shadow: 0 4px 16px rgba(0, 0, 0, 0.5) !important;
                    border: 1px solid rgba(255, 255, 255, 0.2) !important;
                }

                #notificationTooltip.show {
                    opacity: 1 !important;
                    visibility: visible !important;
                }

                /* Mobile Responsive for Notification Toggle - LEFT of Request button */
                @media screen and (max-width: 925px) {
                    #notificationToggle {
                        position: absolute !important;
                        top: 55px !important;
                        left: auto !important;
                        right: 150px !important;
                    }
                }

                @media screen and (max-width: 590px) {
                    #notificationToggle {
                        position: absolute !important;
                        top: 58px !important;
                        right: 130px !important;
                    }

                    #notificationToggleIcon {
                        font-size: 16px !important;
                    }

                    #notificationToggle.disabled::before,
                    #notificationToggle.disabled::after {
                        height: 20px !important;
                    }
                }

                @media screen and (max-width: 470px) {
                    #notificationToggle {
                        position: absolute !important;
                        top: 12px !important;
                        right: 180px !important;
                    }

                    #notificationToggleIcon {
                        font-size: 16px !important;
                    }
                }

                /* Request Modal - Completely Isolated */
                #requestMediaModal {
                    position: fixed !important;
                    top: 0 !important;
                    left: 0 !important;
                    width: 100% !important;
                    height: 100% !important;
                    background: rgba(0, 0, 0, 0.8) !important;
                    z-index: 9999999 !important;
                    display: none !important;
                    align-items: center !important;
                    justify-content: center !important;
                    font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif !important;
                }

                #requestMediaModal.show {
                    display: flex !important;
                }

                #requestMediaModalContent {
                    background: #1e1e1e !important;
                    padding: 30px !important;
                    border-radius: 15px !important;
                    max-width: 900px !important;
                    width: 90% !important;
                    max-height: 80vh !important;
                    overflow-y: auto !important;
                    box-shadow: 0 10px 40px rgba(0, 0, 0, 0.5) !important;
                    position: relative !important;
                }

                #requestMediaModalClose {
                    position: absolute !important;
                    top: 15px !important;
                    right: 15px !important;
                    font-size: 28px !important;
                    color: #999 !important;
                    cursor: pointer !important;
                    background: none !important;
                    border: none !important;
                    line-height: 1 !important;
                }

                #requestMediaModalClose:hover {
                    color: #fff !important;
                }

                #requestMediaModalTitle {
                    font-size: 24px !important;
                    font-weight: 600 !important;
                    color: #fff !important;
                    margin-bottom: 20px !important;
                }

                #requestMediaModalBody {
                    color: #ccc !important;
                    font-size: 16px !important;
                }

                /* User Request Form */
                .request-input-group {
                    margin-bottom: 20px !important;
                }

                .request-input-group label {
                    display: block !important;
                    margin-bottom: 8px !important;
                    color: #fff !important;
                    font-weight: 500 !important;
                }

                .request-input-group input,
                .request-input-group textarea {
                    width: 100% !important;
                    padding: 12px !important;
                    background: #2a2a2a !important;
                    border: 1px solid #444 !important;
                    border-radius: 8px !important;
                    color: #fff !important;
                    font-size: 14px !important;
                    font-family: inherit !important;
                    box-sizing: border-box !important;
                }

                .request-input-group textarea {
                    min-height: 100px !important;
                    resize: vertical !important;
                }

                .request-input-group select {
                    width: 100% !important;
                    padding: 12px !important;
                    background: #2a2a2a !important;
                    border: 1px solid #444 !important;
                    border-radius: 8px !important;
                    color: #fff !important;
                    font-size: 14px !important;
                    font-family: inherit !important;
                    box-sizing: border-box !important;
                    cursor: pointer !important;
                }

                .request-input-group select option {
                    background: #2a2a2a !important;
                    color: #fff !important;
                }

                .request-description {
                    background: #2a2a2a !important;
                    border: 1px solid #667eea !important;
                    border-radius: 8px !important;
                    padding: 15px !important;
                    margin-bottom: 25px !important;
                    color: #ccc !important;
                    font-size: 14px !important;
                    line-height: 1.6 !important;
                }

                .request-description strong {
                    color: #fff !important;
                }

                .request-submit-btn {
                    background: linear-gradient(135deg, #667eea 0%, #764ba2 100%) !important;
                    color: white !important;
                    border: none !important;
                    padding: 12px 30px !important;
                    border-radius: 25px !important;
                    font-size: 14px !important;
                    font-weight: 600 !important;
                    cursor: pointer !important;
                    transition: all 0.3s ease !important;
                    width: 100% !important;
                }

                .request-submit-btn:hover {
                    transform: translateY(-2px) !important;
                    box-shadow: 0 6px 20px rgba(0, 0, 0, 0.4) !important;
                }

                /* Language Toggle Container */
                .language-toggle-container {
                    display: flex !important;
                    align-items: center !important;
                    justify-content: center !important;
                    gap: 10px !important;
                    margin-bottom: 15px !important;
                    padding: 10px !important;
                    background: rgba(255, 255, 255, 0.05) !important;
                    border-radius: 8px !important;
                }

                .lang-label {
                    font-size: 13px !important;
                    font-weight: 600 !important;
                    color: #aaa !important;
                    min-width: 20px !important;
                    text-align: center !important;
                }

                .language-switch {
                    position: relative !important;
                    display: inline-block !important;
                    width: 50px !important;
                    height: 26px !important;
                }

                .language-switch input {
                    opacity: 0 !important;
                    width: 0 !important;
                    height: 0 !important;
                }

                .lang-slider {
                    position: absolute !important;
                    cursor: pointer !important;
                    top: 0 !important;
                    left: 0 !important;
                    right: 0 !important;
                    bottom: 0 !important;
                    background: linear-gradient(135deg, #667eea 0%, #764ba2 100%) !important;
                    transition: 0.3s !important;
                    border-radius: 26px !important;
                }

                .lang-slider:before {
                    position: absolute !important;
                    content: "" !important;
                    height: 20px !important;
                    width: 20px !important;
                    left: 3px !important;
                    bottom: 3px !important;
                    background: white !important;
                    transition: 0.3s !important;
                    border-radius: 50% !important;
                }

                .language-switch input:checked + .lang-slider:before {
                    transform: translateX(24px) !important;
                }

                /* Admin Request List - Modern Card Style */
                .admin-request-list {
                    list-style: none !important;
                    padding: 0 !important;
                    margin: 0 !important;
                    display: flex !important;
                    flex-direction: column !important;
                    gap: 12px !important;
                }

                .admin-request-item {
                    background: linear-gradient(145deg, #2a2a2a 0%, #1e1e1e 100%) !important;
                    border: 1px solid #3a3a3a !important;
                    border-radius: 12px !important;
                    padding: 0 !important;
                    overflow: hidden !important;
                    transition: all 0.3s cubic-bezier(0.4, 0, 0.2, 1) !important;
                    box-shadow: 0 2px 8px rgba(0,0,0,0.2) !important;
                }

                .admin-request-item:hover {
                    border-color: #00a4dc !important;
                    box-shadow: 0 4px 20px rgba(0,164,220,0.15) !important;
                    transform: translateY(-2px) !important;
                }

                /* Card Header */
                .admin-request-header {
                    display: flex !important;
                    align-items: center !important;
                    justify-content: space-between !important;
                    padding: 16px 20px !important;
                    background: rgba(255,255,255,0.03) !important;
                    border-bottom: 1px solid #333 !important;
                    gap: 16px !important;
                }

                .admin-request-header-left {
                    display: flex !important;
                    align-items: center !important;
                    gap: 16px !important;
                    flex: 1 !important;
                    min-width: 0 !important;
                }

                .admin-request-title {
                    color: #fff !important;
                    font-weight: 600 !important;
                    font-size: 15px !important;
                    overflow: hidden !important;
                    text-overflow: ellipsis !important;
                    white-space: nowrap !important;
                    max-width: 250px !important;
                }

                .admin-request-meta {
                    display: flex !important;
                    align-items: center !important;
                    gap: 12px !important;
                    flex-shrink: 0 !important;
                }

                .admin-request-user {
                    color: #888 !important;
                    font-size: 12px !important;
                    background: rgba(255,255,255,0.08) !important;
                    padding: 4px 10px !important;
                    border-radius: 20px !important;
                }

                .admin-request-type {
                    color: #00a4dc !important;
                    font-size: 11px !important;
                    font-weight: 600 !important;
                    text-transform: uppercase !important;
                    letter-spacing: 0.5px !important;
                }

                .admin-request-header-right {
                    display: flex !important;
                    align-items: center !important;
                    gap: 12px !important;
                }

                .admin-request-date {
                    color: #666 !important;
                    font-size: 11px !important;
                    display: flex !important;
                    align-items: center !important;
                    gap: 4px !important;
                }

                .admin-request-status-badge {
                    padding: 6px 14px !important;
                    border-radius: 20px !important;
                    font-size: 11px !important;
                    font-weight: 700 !important;
                    text-transform: uppercase !important;
                    letter-spacing: 0.5px !important;
                    text-align: center !important;
                }

                .admin-request-status-badge.pending {
                    background: linear-gradient(135deg, #ff9800 0%, #f57c00 100%) !important;
                    color: #000 !important;
                    box-shadow: 0 2px 8px rgba(255,152,0,0.3) !important;
                }

                .admin-request-status-badge.processing {
                    background: linear-gradient(135deg, #2196F3 0%, #1976D2 100%) !important;
                    color: #fff !important;
                    box-shadow: 0 2px 8px rgba(33,150,243,0.3) !important;
                }

                .admin-request-status-badge.done {
                    background: linear-gradient(135deg, #4CAF50 0%, #388E3C 100%) !important;
                    color: #fff !important;
                    box-shadow: 0 2px 8px rgba(76,175,80,0.3) !important;
                }

                .admin-request-status-badge.rejected {
                    background: linear-gradient(135deg, #f44336 0%, #d32f2f 100%) !important;
                    color: #fff !important;
                    box-shadow: 0 2px 8px rgba(244,67,54,0.3) !important;
                }

                .admin-request-status-badge.snoozed {
                    background: linear-gradient(135deg, #9c27b0 0%, #7b1fa2 100%) !important;
                    color: #fff !important;
                    box-shadow: 0 2px 8px rgba(156,39,176,0.3) !important;
                }

                /* Card Body */
                .admin-request-body {
                    padding: 16px 20px !important;
                    display: flex !important;
                    flex-direction: column !important;
                    gap: 12px !important;
                }

                .admin-request-info-row {
                    display: flex !important;
                    flex-wrap: wrap !important;
                    gap: 16px !important;
                    align-items: center !important;
                }

                .admin-request-details {
                    color: #aaa !important;
                    font-size: 13px !important;
                    line-height: 1.4 !important;
                }

                .admin-request-imdb {
                    display: inline-flex !important;
                    align-items: center !important;
                    gap: 6px !important;
                    background: rgba(245,197,24,0.15) !important;
                    padding: 4px 10px !important;
                    border-radius: 6px !important;
                    font-size: 12px !important;
                }

                .admin-request-imdb a {
                    color: #f5c518 !important;
                    text-decoration: none !important;
                    font-weight: 600 !important;
                }

                .admin-request-imdb a:hover {
                    text-decoration: underline !important;
                }

                .admin-request-completed {
                    display: flex !important;
                    align-items: center !important;
                    gap: 8px !important;
                    color: #4CAF50 !important;
                    font-size: 12px !important;
                }

                .admin-request-watch-btn {
                    display: inline-flex !important;
                    align-items: center !important;
                    gap: 6px !important;
                    background: linear-gradient(135deg, #4CAF50 0%, #388E3C 100%) !important;
                    color: #fff !important;
                    padding: 8px 16px !important;
                    border-radius: 8px !important;
                    font-size: 12px !important;
                    font-weight: 600 !important;
                    text-decoration: none !important;
                    transition: all 0.2s ease !important;
                }

                .admin-request-watch-btn:hover {
                    transform: scale(1.05) !important;
                    box-shadow: 0 4px 12px rgba(76,175,80,0.4) !important;
                }

                /* Card Actions */
                .admin-request-actions {
                    padding: 16px 20px !important;
                    background: rgba(0,0,0,0.2) !important;
                    border-top: 1px solid #333 !important;
                    display: flex !important;
                    flex-direction: column !important;
                    gap: 12px !important;
                }

                .admin-actions-row {
                    display: flex !important;
                    align-items: center !important;
                    gap: 8px !important;
                    flex-wrap: wrap !important;
                }

                .admin-actions-label {
                    color: #666 !important;
                    font-size: 11px !important;
                    text-transform: uppercase !important;
                    letter-spacing: 0.5px !important;
                    min-width: 80px !important;
                }

                .admin-status-btn {
                    padding: 8px 16px !important;
                    border: none !important;
                    border-radius: 8px !important;
                    font-size: 12px !important;
                    font-weight: 600 !important;
                    cursor: pointer !important;
                    transition: all 0.2s cubic-bezier(0.4, 0, 0.2, 1) !important;
                    white-space: nowrap !important;
                }

                .admin-status-btn.pending {
                    background: rgba(255, 152, 0, 0.15) !important;
                    color: #ffb74d !important;
                    border: 1px solid rgba(255, 152, 0, 0.3) !important;
                }

                .admin-status-btn.pending:hover {
                    background: rgba(255, 152, 0, 0.3) !important;
                    border-color: #ff9800 !important;
                    transform: translateY(-2px) !important;
                }

                .admin-status-btn.processing {
                    background: rgba(33, 150, 243, 0.15) !important;
                    color: #64b5f6 !important;
                    border: 1px solid rgba(33, 150, 243, 0.3) !important;
                }

                .admin-status-btn.processing:hover {
                    background: rgba(33, 150, 243, 0.3) !important;
                    border-color: #2196F3 !important;
                    transform: translateY(-2px) !important;
                }

                .admin-status-btn.done {
                    background: rgba(76, 175, 80, 0.15) !important;
                    color: #81c784 !important;
                    border: 1px solid rgba(76, 175, 80, 0.3) !important;
                }

                .admin-status-btn.done:hover {
                    background: rgba(76, 175, 80, 0.3) !important;
                    border-color: #4CAF50 !important;
                    transform: translateY(-2px) !important;
                }

                .admin-status-btn.rejected {
                    background: rgba(244, 67, 54, 0.15) !important;
                    color: #e57373 !important;
                    border: 1px solid rgba(244, 67, 54, 0.3) !important;
                }

                .admin-status-btn.rejected:hover {
                    background: rgba(244, 67, 54, 0.3) !important;
                    border-color: #f44336 !important;
                    transform: translateY(-2px) !important;
                }

                .admin-delete-btn {
                    padding: 8px 12px !important;
                    background: rgba(244, 67, 54, 0.1) !important;
                    border: 1px solid rgba(244, 67, 54, 0.2) !important;
                    border-radius: 8px !important;
                    color: #e57373 !important;
                    cursor: pointer !important;
                    transition: all 0.2s ease !important;
                    font-size: 14px !important;
                }

                .admin-delete-btn:hover {
                    background: rgba(244, 67, 54, 0.25) !important;
                    border-color: #f44336 !important;
                    transform: scale(1.1) !important;
                }

                /* Input Fields */
                .admin-input-group {
                    display: flex !important;
                    align-items: center !important;
                    gap: 8px !important;
                    flex: 1 !important;
                }

                .admin-link-input,
                .admin-rejection-input {
                    flex: 1 !important;
                    padding: 10px 14px !important;
                    border-radius: 8px !important;
                    border: 1px solid #444 !important;
                    background: #1a1a1a !important;
                    color: #fff !important;
                    font-size: 13px !important;
                    transition: all 0.2s ease !important;
                }

                .admin-link-input:focus,
                .admin-rejection-input:focus {
                    border-color: #00a4dc !important;
                    outline: none !important;
                    box-shadow: 0 0 0 3px rgba(0,164,220,0.15) !important;
                }

                .admin-link-input::placeholder,
                .admin-rejection-input::placeholder {
                    color: #555 !important;
                }

                /* Snooze Controls */
                .admin-snooze-row {
                    display: flex !important;
                    align-items: center !important;
                    gap: 8px !important;
                    padding-top: 8px !important;
                    border-top: 1px dashed #333 !important;
                }

                .admin-snooze-date {
                    padding: 8px 12px !important;
                    border-radius: 8px !important;
                    border: 1px solid #444 !important;
                    background: #1a1a1a !important;
                    color: #fff !important;
                    font-size: 13px !important;
                    cursor: pointer !important;
                    transition: all 0.2s ease !important;
                }

                .admin-snooze-date:focus {
                    border-color: #9c27b0 !important;
                    outline: none !important;
                }

                .admin-snooze-date::-webkit-calendar-picker-indicator {
                    filter: invert(1) !important;
                    cursor: pointer !important;
                }

                .admin-snooze-btn {
                    padding: 8px 16px !important;
                    background: rgba(156, 39, 176, 0.15) !important;
                    border: 1px solid rgba(156, 39, 176, 0.3) !important;
                    border-radius: 8px !important;
                    color: #ce93d8 !important;
                    font-size: 12px !important;
                    font-weight: 600 !important;
                    cursor: pointer !important;
                    transition: all 0.2s ease !important;
                }

                .admin-snooze-btn:hover {
                    background: rgba(156, 39, 176, 0.3) !important;
                    border-color: #9c27b0 !important;
                    transform: translateY(-2px) !important;
                }

                .admin-unsnooze-btn {
                    padding: 8px 16px !important;
                    background: rgba(255, 152, 0, 0.15) !important;
                    border: 1px solid rgba(255, 152, 0, 0.3) !important;
                    border-radius: 8px !important;
                    color: #ffb74d !important;
                    font-size: 12px !important;
                    font-weight: 600 !important;
                    cursor: pointer !important;
                    transition: all 0.2s ease !important;
                }

                .admin-unsnooze-btn:hover {
                    background: rgba(255, 152, 0, 0.3) !important;
                    border-color: #ff9800 !important;
                    transform: translateY(-2px) !important;
                }

                .admin-snooze-info {
                    color: #ce93d8 !important;
                    font-size: 12px !important;
                    display: flex !important;
                    align-items: center !important;
                    gap: 6px !important;
                    background: rgba(156, 39, 176, 0.1) !important;
                    padding: 6px 12px !important;
                    border-radius: 6px !important;
                }

                /* Rejection display */
                .admin-rejection-reason {
                    color: #e57373 !important;
                    font-size: 12px !important;
                    background: rgba(244, 67, 54, 0.1) !important;
                    padding: 8px 12px !important;
                    border-radius: 6px !important;
                    border-left: 3px solid #f44336 !important;
                }

                .admin-request-custom-field {
                    color: #9c9 !important;
                    font-size: 12px !important;
                    background: rgba(153, 204, 153, 0.1) !important;
                    padding: 4px 8px !important;
                    border-radius: 4px !important;
                    display: inline-block !important;
                }

                .snoozed-item {
                    border-color: #9c27b0 !important;
                    background: linear-gradient(145deg, rgba(156, 39, 176, 0.1) 0%, #1e1e1e 100%) !important;
                }

                /* Hide mobile elements on desktop */
                .admin-status-select,
                .mobile-delete {
                    display: none !important;
                }

                /* ============================================
                   COLLAPSIBLE CATEGORY SECTIONS
                   ============================================ */
                .admin-category-section {
                    margin-bottom: 8px !important;
                    border-radius: 12px !important;
                    overflow: hidden !important;
                    background: #1a1a1a !important;
                    border: 1px solid #333 !important;
                    transition: all 0.3s ease !important;
                }

                .admin-category-section:hover {
                    border-color: #444 !important;
                }

                .admin-category-header {
                    color: #fff !important;
                    font-size: 14px !important;
                    font-weight: 600 !important;
                    margin: 0 !important;
                    padding: 14px 20px !important;
                    background: linear-gradient(135deg, rgba(255,255,255,0.05) 0%, transparent 100%) !important;
                    cursor: pointer !important;
                    display: flex !important;
                    align-items: center !important;
                    justify-content: space-between !important;
                    user-select: none !important;
                    transition: all 0.2s ease !important;
                }

                .admin-category-header:hover {
                    background: rgba(255,255,255,0.08) !important;
                }

                .admin-category-header-left {
                    display: flex !important;
                    align-items: center !important;
                    gap: 12px !important;
                    flex: 1 !important;
                }

                .admin-category-icon {
                    width: 32px !important;
                    height: 32px !important;
                    border-radius: 8px !important;
                    display: flex !important;
                    align-items: center !important;
                    justify-content: center !important;
                    font-size: 14px !important;
                }

                .admin-category-count {
                    background: rgba(255,255,255,0.15) !important;
                    padding: 4px 12px !important;
                    border-radius: 20px !important;
                    font-size: 12px !important;
                    font-weight: 700 !important;
                    min-width: 32px !important;
                    text-align: center !important;
                }

                .admin-category-chevron {
                    font-size: 12px !important;
                    transition: transform 0.3s ease !important;
                    color: #666 !important;
                }

                .admin-category-section.expanded .admin-category-chevron {
                    transform: rotate(180deg) !important;
                }

                .admin-category-content {
                    max-height: 0 !important;
                    overflow: hidden !important;
                    transition: max-height 0.4s cubic-bezier(0.4, 0, 0.2, 1) !important;
                }

                .admin-category-section.expanded .admin-category-content {
                    max-height: 2000px !important;
                }

                .admin-category-list {
                    padding: 8px !important;
                    display: flex !important;
                    flex-direction: column !important;
                    gap: 4px !important;
                }

                /* Category Colors */
                .admin-category-section[data-category="processing"] .admin-category-icon {
                    background: rgba(33, 150, 243, 0.2) !important;
                    color: #64b5f6 !important;
                }
                .admin-category-section[data-category="processing"] .admin-category-count {
                    background: rgba(33, 150, 243, 0.3) !important;
                    color: #90caf9 !important;
                }

                .admin-category-section[data-category="pending"] .admin-category-icon {
                    background: rgba(255, 152, 0, 0.2) !important;
                    color: #ffb74d !important;
                }
                .admin-category-section[data-category="pending"] .admin-category-count {
                    background: rgba(255, 152, 0, 0.3) !important;
                    color: #ffcc80 !important;
                }

                .admin-category-section[data-category="snoozed"] .admin-category-icon {
                    background: rgba(156, 39, 176, 0.2) !important;
                    color: #ce93d8 !important;
                }
                .admin-category-section[data-category="snoozed"] .admin-category-count {
                    background: rgba(156, 39, 176, 0.3) !important;
                    color: #e1bee7 !important;
                }

                .admin-category-section[data-category="done"] .admin-category-icon {
                    background: rgba(76, 175, 80, 0.2) !important;
                    color: #81c784 !important;
                }
                .admin-category-section[data-category="done"] .admin-category-count {
                    background: rgba(76, 175, 80, 0.3) !important;
                    color: #a5d6a7 !important;
                }

                .admin-category-section[data-category="rejected"] .admin-category-icon {
                    background: rgba(244, 67, 54, 0.2) !important;
                    color: #e57373 !important;
                }
                .admin-category-section[data-category="rejected"] .admin-category-count {
                    background: rgba(244, 67, 54, 0.3) !important;
                    color: #ef9a9a !important;
                }

                .admin-category-section[data-category="new"] .admin-category-icon {
                    background: rgba(0, 200, 83, 0.2) !important;
                    color: #69f0ae !important;
                }
                .admin-category-section[data-category="new"] .admin-category-count {
                    background: rgba(0, 200, 83, 0.3) !important;
                    color: #b9f6ca !important;
                }

                /* ============================================
                   COMPACT REQUEST CARD (default state)
                   ============================================ */
                .admin-request-item {
                    background: #222 !important;
                    border-radius: 8px !important;
                    cursor: pointer !important;
                    transition: all 0.2s ease !important;
                    overflow: hidden !important;
                }

                .admin-request-item:hover {
                    background: #2a2a2a !important;
                }

                .admin-request-item.expanded {
                    background: #252525 !important;
                    cursor: default !important;
                }

                /* Compact View */
                .admin-request-compact {
                    display: flex !important;
                    align-items: center !important;
                    padding: 12px 16px !important;
                    gap: 16px !important;
                }

                .admin-request-compact-title {
                    flex: 1 !important;
                    color: #fff !important;
                    font-weight: 500 !important;
                    font-size: 13px !important;
                    overflow: hidden !important;
                    text-overflow: ellipsis !important;
                    white-space: nowrap !important;
                    min-width: 0 !important;
                }

                .admin-request-compact-meta {
                    display: flex !important;
                    align-items: center !important;
                    gap: 12px !important;
                    flex-shrink: 0 !important;
                }

                .admin-request-compact-user {
                    color: #888 !important;
                    font-size: 11px !important;
                    background: rgba(255,255,255,0.08) !important;
                    padding: 3px 8px !important;
                    border-radius: 12px !important;
                }

                .admin-request-compact-type {
                    color: #00a4dc !important;
                    font-size: 10px !important;
                    font-weight: 600 !important;
                    text-transform: uppercase !important;
                }

                .admin-request-compact-date {
                    color: #555 !important;
                    font-size: 10px !important;
                }

                .admin-request-compact-status {
                    padding: 4px 10px !important;
                    border-radius: 12px !important;
                    font-size: 10px !important;
                    font-weight: 700 !important;
                    text-transform: uppercase !important;
                }

                .admin-request-compact-status.pending { background: #ff9800 !important; color: #000 !important; }
                .admin-request-compact-status.processing { background: #2196F3 !important; color: #fff !important; }
                .admin-request-compact-status.done { background: #4CAF50 !important; color: #fff !important; }
                .admin-request-compact-status.rejected { background: #f44336 !important; color: #fff !important; }
                .admin-request-compact-status.snoozed { background: #9c27b0 !important; color: #fff !important; }
                .admin-request-compact-status.new { background: #00c853 !important; color: #000 !important; }

                .admin-request-expand-icon {
                    color: #444 !important;
                    font-size: 10px !important;
                    transition: transform 0.2s ease !important;
                }

                .admin-request-item.expanded .admin-request-expand-icon {
                    transform: rotate(180deg) !important;
                }

                /* ============================================
                   EXPANDED REQUEST DETAILS
                   ============================================ */
                .admin-request-details-panel {
                    max-height: 0 !important;
                    overflow: hidden !important;
                    transition: max-height 0.3s ease !important;
                    border-top: 1px solid transparent !important;
                }

                .admin-request-item.expanded .admin-request-details-panel {
                    max-height: 500px !important;
                    border-top-color: #333 !important;
                }

                .admin-request-details-content {
                    padding: 16px !important;
                    display: flex !important;
                    flex-direction: column !important;
                    gap: 12px !important;
                }

                .admin-request-detail-row {
                    display: flex !important;
                    flex-wrap: wrap !important;
                    gap: 12px !important;
                    align-items: center !important;
                }

                .admin-request-detail-item {
                    display: flex !important;
                    align-items: center !important;
                    gap: 6px !important;
                    font-size: 12px !important;
                    color: #aaa !important;
                }

                .admin-request-detail-item.imdb {
                    background: rgba(245, 197, 24, 0.15) !important;
                    padding: 4px 10px !important;
                    border-radius: 6px !important;
                }

                .admin-request-detail-item.imdb a {
                    color: #f5c518 !important;
                    text-decoration: none !important;
                    font-weight: 600 !important;
                }

                .admin-request-notes {
                    color: #888 !important;
                    font-size: 12px !important;
                    padding: 10px !important;
                    background: rgba(0,0,0,0.2) !important;
                    border-radius: 6px !important;
                    line-height: 1.4 !important;
                }

                .admin-request-rejection {
                    color: #e57373 !important;
                    font-size: 12px !important;
                    padding: 10px !important;
                    background: rgba(244, 67, 54, 0.1) !important;
                    border-radius: 6px !important;
                    border-left: 3px solid #f44336 !important;
                }

                /* Action Buttons Row */
                .admin-request-actions-row {
                    display: flex !important;
                    gap: 6px !important;
                    flex-wrap: wrap !important;
                    padding-top: 8px !important;
                    border-top: 1px dashed #333 !important;
                }

                .admin-action-btn {
                    padding: 8px 14px !important;
                    border: 1px solid !important;
                    border-radius: 6px !important;
                    font-size: 11px !important;
                    font-weight: 600 !important;
                    cursor: pointer !important;
                    transition: all 0.2s ease !important;
                    background: transparent !important;
                }

                .admin-action-btn.pending {
                    border-color: rgba(255, 152, 0, 0.4) !important;
                    color: #ffb74d !important;
                }
                .admin-action-btn.pending:hover {
                    background: rgba(255, 152, 0, 0.2) !important;
                }

                .admin-action-btn.processing {
                    border-color: rgba(33, 150, 243, 0.4) !important;
                    color: #64b5f6 !important;
                }
                .admin-action-btn.processing:hover {
                    background: rgba(33, 150, 243, 0.2) !important;
                }

                .admin-action-btn.done {
                    border-color: rgba(76, 175, 80, 0.4) !important;
                    color: #81c784 !important;
                }
                .admin-action-btn.done:hover {
                    background: rgba(76, 175, 80, 0.2) !important;
                }

                .admin-action-btn.rejected {
                    border-color: rgba(244, 67, 54, 0.4) !important;
                    color: #e57373 !important;
                }
                .admin-action-btn.rejected:hover {
                    background: rgba(244, 67, 54, 0.2) !important;
                }

                .admin-action-btn.delete {
                    border-color: rgba(244, 67, 54, 0.3) !important;
                    color: #e57373 !important;
                    margin-left: auto !important;
                }
                .admin-action-btn.delete:hover {
                    background: rgba(244, 67, 54, 0.2) !important;
                }

                /* Input Fields in Expanded View */
                .admin-request-inputs {
                    display: flex !important;
                    gap: 8px !important;
                    margin-top: 8px !important;
                }

                .admin-request-input {
                    flex: 1 !important;
                    padding: 10px 12px !important;
                    border: 1px solid #333 !important;
                    border-radius: 6px !important;
                    background: #1a1a1a !important;
                    color: #fff !important;
                    font-size: 12px !important;
                    transition: border-color 0.2s ease !important;
                }

                .admin-request-input:focus {
                    border-color: #00a4dc !important;
                    outline: none !important;
                }

                .admin-request-input::placeholder {
                    color: #555 !important;
                }

                /* Snooze Controls */
                .admin-snooze-controls {
                    display: flex !important;
                    align-items: center !important;
                    gap: 8px !important;
                    margin-top: 8px !important;
                    padding-top: 8px !important;
                    border-top: 1px dashed #333 !important;
                }

                .admin-snooze-date {
                    padding: 8px 12px !important;
                    border: 1px solid #333 !important;
                    border-radius: 6px !important;
                    background: #1a1a1a !important;
                    color: #fff !important;
                    font-size: 12px !important;
                }

                .admin-snooze-date::-webkit-calendar-picker-indicator {
                    filter: invert(1) !important;
                }

                .admin-snooze-btn, .admin-unsnooze-btn {
                    padding: 8px 14px !important;
                    border: 1px solid !important;
                    border-radius: 6px !important;
                    font-size: 11px !important;
                    font-weight: 600 !important;
                    cursor: pointer !important;
                    transition: all 0.2s ease !important;
                    background: transparent !important;
                }

                .admin-snooze-btn {
                    border-color: rgba(156, 39, 176, 0.4) !important;
                    color: #ce93d8 !important;
                }
                .admin-snooze-btn:hover {
                    background: rgba(156, 39, 176, 0.2) !important;
                }

                .admin-unsnooze-btn {
                    border-color: rgba(255, 152, 0, 0.4) !important;
                    color: #ffb74d !important;
                }
                .admin-unsnooze-btn:hover {
                    background: rgba(255, 152, 0, 0.2) !important;
                }

                /* Watch Button */
                .admin-watch-btn {
                    display: inline-flex !important;
                    align-items: center !important;
                    gap: 6px !important;
                    padding: 8px 14px !important;
                    background: linear-gradient(135deg, #4CAF50 0%, #388E3C 100%) !important;
                    color: #fff !important;
                    border-radius: 6px !important;
                    font-size: 11px !important;
                    font-weight: 600 !important;
                    text-decoration: none !important;
                    transition: all 0.2s ease !important;
                }

                .admin-watch-btn:hover {
                    transform: scale(1.05) !important;
                    box-shadow: 0 4px 12px rgba(76, 175, 80, 0.3) !important;
                }

                /* Admin Header Row */
                .admin-header-row {
                    display: flex !important;
                    align-items: center !important;
                    justify-content: space-between !important;
                    margin-bottom: 16px !important;
                    padding-bottom: 12px !important;
                    border-bottom: 1px solid #333 !important;
                }

                .admin-new-request-btn {
                    display: inline-flex !important;
                    align-items: center !important;
                    gap: 8px !important;
                    padding: 10px 20px !important;
                    background: linear-gradient(135deg, #00a4dc 0%, #0077b5 100%) !important;
                    color: #fff !important;
                    border: none !important;
                    border-radius: 8px !important;
                    font-size: 13px !important;
                    font-weight: 600 !important;
                    cursor: pointer !important;
                    transition: all 0.2s ease !important;
                    box-shadow: 0 2px 8px rgba(0, 164, 220, 0.3) !important;
                }

                .admin-new-request-btn:hover {
                    transform: translateY(-1px) !important;
                    box-shadow: 0 4px 16px rgba(0, 164, 220, 0.4) !important;
                }

                /* Empty state */
                .admin-request-empty {
                    text-align: center !important;
                    color: #555 !important;
                    padding: 40px 20px !important;
                    font-size: 13px !important;
                }

                /* Hide elements */
                .admin-status-select, .mobile-delete {
                    display: none !important;
                }

                .admin-request-time span {
                    white-space: nowrap !important;
                }

                /* Media Link Input */
                .admin-link-input {
                    padding: 6px 10px !important;
                    border: 1px solid #555 !important;
                    border-radius: 6px !important;
                    background: #2a2a2a !important;
                    color: #fff !important;
                    font-size: 11px !important;
                    width: 100% !important;
                    margin-top: 8px !important;
                }

                .admin-link-input:focus {
                    outline: none !important;
                    border-color: #4CAF50 !important;
                }

                .admin-link-input::placeholder {
                    color: #777 !important;
                }

                /* Media Link Display */
                .request-media-link {
                    display: inline-block !important;
                    margin-top: 5px !important;
                    padding: 4px 10px !important;
                    background: linear-gradient(135deg, #667eea 0%, #764ba2 100%) !important;
                    color: #fff !important;
                    text-decoration: none !important;
                    border-radius: 12px !important;
                    font-size: 11px !important;
                    font-weight: 600 !important;
                    transition: all 0.2s ease !important;
                }

                .request-media-link:hover {
                    transform: scale(1.05) !important;
                    box-shadow: 0 2px 8px rgba(102, 126, 234, 0.4) !important;
                }

                /* Admin Status Dropdown (for mobile) */
                .admin-status-select {
                    display: none !important;
                    padding: 6px 10px !important;
                    border-radius: 8px !important;
                    border: 1px solid #555 !important;
                    background: #333 !important;
                    color: #fff !important;
                    font-size: 12px !important;
                    font-weight: 600 !important;
                    cursor: pointer !important;
                    min-width: 100px !important;
                }

                .admin-status-select:focus {
                    outline: none !important;
                    border-color: #667eea !important;
                }

                .admin-status-select option {
                    background: #333 !important;
                    color: #fff !important;
                    padding: 8px !important;
                }

                /* Mobile Admin Table - Card Layout */
                @media screen and (max-width: 768px) {
                    .admin-request-item {
                        grid-template-columns: 1fr !important;
                        gap: 8px !important;
                        padding: 12px !important;
                    }

                    .admin-request-title {
                        font-size: 13px !important;
                        white-space: normal !important;
                        line-height: 1.3 !important;
                    }

                    .admin-request-user {
                        font-size: 11px !important;
                    }

                    .admin-request-details {
                        font-size: 10px !important;
                        white-space: normal !important;
                        line-height: 1.3 !important;
                    }

                    .admin-request-status-badge {
                        font-size: 10px !important;
                        padding: 3px 8px !important;
                    }

                    /* Hide buttons, show dropdown on mobile */
                    .admin-request-actions {
                        display: none !important;
                    }

                    .admin-status-select {
                        display: block !important;
                        width: 100% !important;
                    }

                    /* Show mobile delete button */
                    .admin-delete-btn.mobile-delete {
                        display: block !important;
                        width: 100% !important;
                        margin-top: 8px !important;
                    }

                    /* Hide desktop delete (inside actions) */
                    .admin-request-actions .admin-delete-btn {
                        display: none !important;
                    }

                    /* Timestamps on mobile */
                    .admin-request-time {
                        font-size: 9px !important;
                    }

                    /* Link input on mobile */
                    .admin-link-input {
                        font-size: 12px !important;
                    }

                    /* Request Modal - Full width on mobile */
                    .request-media-modal .modal-content {
                        width: 95% !important;
                        max-width: none !important;
                        margin: 10px !important;
                    }

                    .request-media-modal .modal-body {
                        max-height: 70vh !important;
                        padding: 15px !important;
                    }

                    /* User request list on mobile */
                    .user-request-item {
                        flex-direction: column !important;
                        align-items: flex-start !important;
                        gap: 8px !important;
                    }

                    .user-request-time {
                        font-size: 9px !important;
                    }

                    .request-media-link {
                        font-size: 10px !important;
                        padding: 3px 8px !important;
                    }
                }

                /* User Request List - Compact Style */
                .user-request-list {
                    list-style: none !important;
                    padding: 0 !important;
                    margin: 15px 0 0 0 !important;
                    max-height: 250px !important;
                    overflow-y: auto !important;
                }

                .user-request-item {
                    background: #2a2a2a !important;
                    border: 1px solid #444 !important;
                    border-radius: 6px !important;
                    padding: 8px 12px !important;
                    margin-bottom: 6px !important;
                    display: flex !important;
                    justify-content: space-between !important;
                    align-items: center !important;
                    gap: 10px !important;
                }

                .user-request-info {
                    flex: 1 !important;
                    min-width: 0 !important;
                }

                .user-request-item-title {
                    color: #fff !important;
                    font-weight: 600 !important;
                    font-size: 13px !important;
                    margin-bottom: 2px !important;
                    overflow: hidden !important;
                    text-overflow: ellipsis !important;
                    white-space: nowrap !important;
                }

                .user-request-item-type {
                    color: #999 !important;
                    font-size: 11px !important;
                }

                .user-request-time {
                    color: #777 !important;
                    font-size: 10px !important;
                    margin-top: 3px !important;
                }

                .user-request-status {
                    padding: 4px 10px !important;
                    border-radius: 10px !important;
                    font-size: 10px !important;
                    font-weight: 600 !important;
                    white-space: nowrap !important;
                    flex-shrink: 0 !important;
                }

                .user-request-status.pending {
                    background: #ff9800 !important;
                    color: #000 !important;
                }

                .user-request-status.processing {
                    background: #2196F3 !important;
                    color: #fff !important;
                }

                .user-request-status.done {
                    background: #4CAF50 !important;
                    color: #fff !important;
                }

                .user-request-status.rejected {
                    background: #f44336 !important;
                    color: #fff !important;
                }

                .user-request-rejection-reason {
                    color: #f44336 !important;
                    font-size: 12px !important;
                    margin-top: 6px !important;
                    font-style: italic !important;
                    padding: 6px 10px !important;
                    background: rgba(244, 67, 54, 0.1) !important;
                    border-radius: 4px !important;
                }

                .user-request-custom-field {
                    color: #9c9 !important;
                    font-size: 12px !important;
                    margin-top: 2px !important;
                }

                .user-request-imdb {
                    color: #f5c518 !important;
                    font-size: 12px !important;
                    margin-top: 2px !important;
                }

                .user-request-imdb .imdb-link,
                .admin-request-imdb .imdb-link {
                    color: #f5c518 !important;
                    text-decoration: none !important;
                }

                .user-request-imdb .imdb-link:hover,
                .admin-request-imdb .imdb-link:hover {
                    text-decoration: underline !important;
                }

                .admin-tabs {
                    display: flex !important;
                    gap: 0 !important;
                    margin-bottom: 15px !important;
                    border-bottom: 2px solid #444 !important;
                }

                .admin-tab {
                    padding: 10px 20px !important;
                    background: transparent !important;
                    border: none !important;
                    color: #999 !important;
                    cursor: pointer !important;
                    font-size: 14px !important;
                    font-weight: 500 !important;
                    transition: all 0.2s ease !important;
                    border-bottom: 2px solid transparent !important;
                    margin-bottom: -2px !important;
                    position: relative !important;
                }

                .admin-tab-badge {
                    display: inline-flex !important;
                    align-items: center !important;
                    justify-content: center !important;
                    min-width: 18px !important;
                    height: 18px !important;
                    padding: 0 5px !important;
                    margin-left: 6px !important;
                    background: #e53935 !important;
                    color: #fff !important;
                    font-size: 11px !important;
                    font-weight: 700 !important;
                    border-radius: 9px !important;
                    line-height: 1 !important;
                }

                .admin-tab:hover {
                    color: #fff !important;
                }

                .admin-tab.active {
                    color: #00a4dc !important;
                    border-bottom-color: #00a4dc !important;
                }

                .admin-tab-content {
                    min-height: 200px !important;
                }

                /* Deletion Request Styles */
                .deletion-request-btn {
                    display: inline-block !important;
                    margin-top: 5px !important;
                    margin-left: 6px !important;
                    padding: 4px 10px !important;
                    background: linear-gradient(135deg, #e53935 0%, #c62828 100%) !important;
                    color: #fff !important;
                    text-decoration: none !important;
                    border: none !important;
                    border-radius: 12px !important;
                    font-size: 11px !important;
                    font-weight: 600 !important;
                    cursor: pointer !important;
                    transition: all 0.2s ease !important;
                }

                .deletion-request-btn:hover {
                    transform: scale(1.05) !important;
                    box-shadow: 0 2px 8px rgba(229, 57, 53, 0.4) !important;
                }

                .deletion-request-btn.delete-request-type {
                    background: linear-gradient(135deg, #ff9800 0%, #f57c00 100%) !important;
                }

                .deletion-request-btn.delete-request-type:hover {
                    box-shadow: 0 2px 8px rgba(255, 152, 0, 0.4) !important;
                }

                .deletion-requested-text {
                    display: inline-block !important;
                    margin-top: 5px !important;
                    margin-left: 6px !important;
                    padding: 4px 10px !important;
                    background: #555 !important;
                    color: #ccc !important;
                    border-radius: 12px !important;
                    font-size: 11px !important;
                    font-weight: 600 !important;
                }

                .deletion-rejected-text {
                    display: inline-block !important;
                    margin-top: 5px !important;
                    margin-left: 6px !important;
                    padding: 4px 10px !important;
                    background: rgba(244, 67, 54, 0.2) !important;
                    color: #f44336 !important;
                    border-radius: 12px !important;
                    font-size: 11px !important;
                    font-weight: 600 !important;
                }

                /* Rejection Reason Modal */
                .rejection-modal-overlay {
                    position: fixed !important;
                    top: 0 !important;
                    left: 0 !important;
                    width: 100% !important;
                    height: 100% !important;
                    background: rgba(0, 0, 0, 0.7) !important;
                    z-index: 999999999 !important;
                    display: flex !important;
                    align-items: center !important;
                    justify-content: center !important;
                }

                .rejection-modal {
                    background: #1a1a1a !important;
                    border: 1px solid rgba(255, 255, 255, 0.15) !important;
                    border-radius: 12px !important;
                    padding: 24px !important;
                    width: 400px !important;
                    max-width: 90vw !important;
                    box-shadow: 0 8px 32px rgba(0, 0, 0, 0.6) !important;
                }

                .rejection-modal-title {
                    font-size: 16px !important;
                    font-weight: 600 !important;
                    color: #fff !important;
                    margin-bottom: 16px !important;
                }

                .rejection-modal textarea {
                    width: 100% !important;
                    min-height: 80px !important;
                    background: #2a2a2a !important;
                    border: 1px solid rgba(255, 255, 255, 0.1) !important;
                    border-radius: 8px !important;
                    color: #fff !important;
                    padding: 10px !important;
                    font-size: 13px !important;
                    resize: vertical !important;
                    font-family: inherit !important;
                    box-sizing: border-box !important;
                }

                .rejection-modal textarea:focus {
                    outline: none !important;
                    border-color: #e53935 !important;
                }

                .rejection-modal-buttons {
                    display: flex !important;
                    justify-content: flex-end !important;
                    gap: 10px !important;
                    margin-top: 16px !important;
                }

                .rejection-modal-btn {
                    padding: 8px 20px !important;
                    border: none !important;
                    border-radius: 8px !important;
                    font-size: 13px !important;
                    font-weight: 600 !important;
                    cursor: pointer !important;
                    transition: all 0.2s ease !important;
                }

                .rejection-modal-btn.cancel {
                    background: #333 !important;
                    color: #ccc !important;
                }

                .rejection-modal-btn.cancel:hover {
                    background: #444 !important;
                }

                .rejection-modal-btn.confirm {
                    background: #e53935 !important;
                    color: #fff !important;
                }

                .rejection-modal-btn.confirm:hover {
                    background: #c62828 !important;
                }

                /* Rejection Reason View (user side) */
                .rejection-reason-popup {
                    background: #1a1a1a !important;
                    border: 1px solid rgba(255, 255, 255, 0.15) !important;
                    border-radius: 12px !important;
                    padding: 20px 24px !important;
                    width: 380px !important;
                    max-width: 90vw !important;
                    box-shadow: 0 8px 32px rgba(0, 0, 0, 0.6) !important;
                }

                .rejection-reason-popup-title {
                    font-size: 14px !important;
                    font-weight: 600 !important;
                    color: #f44336 !important;
                    margin-bottom: 12px !important;
                }

                .rejection-reason-popup-text {
                    font-size: 13px !important;
                    color: #ddd !important;
                    line-height: 1.5 !important;
                    background: #2a2a2a !important;
                    padding: 12px !important;
                    border-radius: 8px !important;
                    border-left: 3px solid #e53935 !important;
                }

                .rejection-reason-popup-close {
                    display: block !important;
                    margin-top: 14px !important;
                    margin-left: auto !important;
                    padding: 6px 18px !important;
                    background: #333 !important;
                    color: #ccc !important;
                    border: none !important;
                    border-radius: 8px !important;
                    font-size: 13px !important;
                    cursor: pointer !important;
                }

                .rejection-reason-popup-close:hover {
                    background: #444 !important;
                }

                /* Ban System Styles */
                .ban-section {
                    margin-top: 16px !important;
                    padding-top: 16px !important;
                    border-top: 1px solid rgba(255,255,255,0.1) !important;
                }

                .ban-section-title {
                    font-size: 13px !important;
                    font-weight: 600 !important;
                    color: #999 !important;
                    margin-bottom: 10px !important;
                }

                .ban-item {
                    display: flex !important;
                    align-items: center !important;
                    justify-content: space-between !important;
                    padding: 8px 12px !important;
                    margin-bottom: 6px !important;
                    background: rgba(229, 57, 53, 0.1) !important;
                    border-radius: 8px !important;
                    border-left: 3px solid #e53935 !important;
                }

                .ban-item-info {
                    font-size: 12px !important;
                    color: #ddd !important;
                }

                .ban-item-meta {
                    font-size: 10px !important;
                    color: #999 !important;
                    margin-top: 2px !important;
                }

                .ban-btn {
                    padding: 3px 10px !important;
                    border: none !important;
                    border-radius: 6px !important;
                    font-size: 11px !important;
                    font-weight: 600 !important;
                    cursor: pointer !important;
                    transition: all 0.2s ease !important;
                }

                .ban-btn.ban {
                    background: #e53935 !important;
                    color: #fff !important;
                }

                .ban-btn.ban:hover {
                    background: #c62828 !important;
                }

                .ban-btn.unban {
                    background: #4caf50 !important;
                    color: #fff !important;
                }

                .ban-btn.unban:hover {
                    background: #388e3c !important;
                }

                .ban-dropdown {
                    padding: 3px 6px !important;
                    background: #333 !important;
                    color: #fff !important;
                    border: 1px solid rgba(255,255,255,0.1) !important;
                    border-radius: 6px !important;
                    font-size: 11px !important;
                    margin-right: 6px !important;
                }

                .ban-actions {
                    display: flex !important;
                    align-items: center !important;
                    gap: 4px !important;
                }

                .ban-notice {
                    padding: 12px !important;
                    background: rgba(229, 57, 53, 0.15) !important;
                    border: 1px solid rgba(229, 57, 53, 0.3) !important;
                    border-radius: 8px !important;
                    color: #f44336 !important;
                    font-size: 13px !important;
                    text-align: center !important;
                    margin-bottom: 12px !important;
                }

                .deletion-request-item {
                    display: flex !important;
                    justify-content: space-between !important;
                    align-items: center !important;
                    padding: 12px !important;
                    margin-bottom: 8px !important;
                    background: #1a1a1a !important;
                    border-radius: 8px !important;
                    border-left: 3px solid #e53935 !important;
                }

                .deletion-request-item.resolved {
                    border-left-color: #666 !important;
                    opacity: 0.7 !important;
                }

                .deletion-request-info {
                    flex: 1 !important;
                }

                .deletion-request-title {
                    font-size: 14px !important;
                    font-weight: 600 !important;
                    color: #fff !important;
                }

                .deletion-request-meta {
                    font-size: 11px !important;
                    color: #999 !important;
                    margin-top: 4px !important;
                }

                .deletion-request-user {
                    color: #00a4dc !important;
                }

                .deletion-request-actions {
                    display: flex !important;
                    gap: 4px !important;
                    flex-wrap: wrap !important;
                    margin-top: 8px !important;
                }

                .deletion-action-btn {
                    padding: 4px 10px !important;
                    border: none !important;
                    border-radius: 6px !important;
                    font-size: 11px !important;
                    font-weight: 600 !important;
                    cursor: pointer !important;
                    transition: all 0.2s ease !important;
                }

                .deletion-action-btn.approve {
                    background: #e53935 !important;
                    color: #fff !important;
                }

                .deletion-action-btn.approve:hover {
                    background: #c62828 !important;
                }

                .deletion-action-btn.schedule {
                    background: #ff9800 !important;
                    color: #fff !important;
                }

                .deletion-action-btn.schedule:hover {
                    background: #f57c00 !important;
                }

                .deletion-action-btn.reject {
                    background: #444 !important;
                    color: #ccc !important;
                }

                .deletion-action-btn.reject:hover {
                    background: #555 !important;
                }

                .deletion-status-badge {
                    display: inline-block !important;
                    padding: 2px 8px !important;
                    border-radius: 10px !important;
                    font-size: 10px !important;
                    font-weight: 700 !important;
                    text-transform: uppercase !important;
                }

                .deletion-status-badge.pending {
                    background: #ff9800 !important;
                    color: #fff !important;
                }

                .deletion-status-badge.approved {
                    background: #e53935 !important;
                    color: #fff !important;
                }

                .deletion-status-badge.rejected {
                    background: #666 !important;
                    color: #ccc !important;
                }

                .deletion-request-link {
                    color: #667eea !important;
                    text-decoration: none !important;
                    font-size: 11px !important;
                }

                .deletion-request-link:hover {
                    text-decoration: underline !important;
                }

                .user-request-actions {
                    display: flex !important;
                    gap: 8px !important;
                    margin-top: 10px !important;
                }

                .user-edit-btn,
                .user-delete-btn {
                    padding: 6px 12px !important;
                    border: none !important;
                    border-radius: 4px !important;
                    cursor: pointer !important;
                    font-size: 12px !important;
                    transition: all 0.2s ease !important;
                }

                .user-edit-btn {
                    background: #4a90d9 !important;
                    color: #fff !important;
                }

                .user-edit-btn:hover {
                    background: #3a7bc8 !important;
                }

                .user-delete-btn {
                    background: #d94a4a !important;
                    color: #fff !important;
                }

                .user-delete-btn:hover {
                    background: #c83a3a !important;
                }

                .admin-request-imdb {
                    color: #f5c518 !important;
                    font-size: 11px !important;
                    margin-top: 2px !important;
                }

                .user-requests-title {
                    color: #fff !important;
                    font-weight: 600 !important;
                    font-size: 15px !important;
                    margin-top: 20px !important;
                    margin-bottom: 8px !important;
                    padding-top: 15px !important;
                    border-top: 1px solid #444 !important;
                }

                /* Netflix-Style View Styles */
                .netflix-view-container {
                    padding: 20px 0 !important;
                    background: #141414 !important;
                    position: fixed !important;
                    top: 56px !important;
                    left: 0 !important;
                    right: 0 !important;
                    bottom: 0 !important;
                    width: 100% !important;
                    overflow-y: auto !important;
                    z-index: 100 !important;
                    display: block !important;
                    visibility: visible !important;
                    opacity: 1 !important;
                }

                .netflix-genre-row {
                    margin-bottom: 30px;
                    position: relative;
                }

                .netflix-genre-title {
                    color: #fff;
                    font-size: 1.4em;
                    font-weight: 700;
                    margin-bottom: 12px;
                    padding-left: 4%;
                    text-shadow: 2px 2px 4px rgba(0, 0, 0, 0.5);
                }

                .netflix-row-wrapper {
                    position: relative;
                    overflow: hidden;
                }

                .netflix-row-content {
                    display: flex;
                    overflow-x: auto;
                    scroll-behavior: smooth;
                    gap: 8px;
                    padding: 10px 4%;
                    scrollbar-width: none;
                    -ms-overflow-style: none;
                }

                .netflix-row-content::-webkit-scrollbar {
                    display: none;
                }

                .netflix-card {
                    flex: 0 0 auto;
                    width: 200px;
                    height: 300px;
                    border-radius: 4px;
                    overflow: hidden;
                    position: relative;
                    cursor: pointer;
                    transition: transform 0.3s ease;
                    background: #2a2a2a;
                }

                .netflix-card:hover {
                    transform: scale(1.08);
                    z-index: 50;
                }

                .netflix-card img {
                    width: 100%;
                    height: 100%;
                    object-fit: cover;
                }

                .netflix-card-overlay {
                    position: absolute;
                    bottom: 0;
                    left: 0;
                    right: 0;
                    background: linear-gradient(transparent, rgba(0, 0, 0, 0.9));
                    padding: 40px 10px 10px;
                    opacity: 0;
                    transition: opacity 0.3s ease;
                }

                .netflix-card:hover .netflix-card-overlay {
                    opacity: 1;
                }

                .netflix-card-title {
                    color: #fff;
                    font-size: 14px;
                    font-weight: 600;
                    white-space: nowrap;
                    overflow: hidden;
                    text-overflow: ellipsis;
                }

                .netflix-card-rating {
                    color: #ffd700;
                    font-size: 12px;
                    margin-top: 4px;
                }

                /* Rating badge on Netflix cards */
                .netflix-card.has-rating::after {
                    content: attr(data-rating);
                    position: absolute;
                    top: 8px;
                    left: 8px;
                    background: rgba(0, 0, 0, 0.85);
                    color: #fff;
                    padding: 4px 8px;
                    border-radius: 4px;
                    font-size: 0.85em;
                    z-index: 10;
                    pointer-events: none;
                    font-weight: 600;
                }

                /* Leaving badge on Netflix cards - top right */
                .netflix-card.has-leaving::before {
                    content: attr(data-leaving);
                    position: absolute;
                    top: 8px;
                    right: 8px;
                    background: rgba(231, 76, 60, 0.95);
                    color: #fff;
                    padding: 4px 8px;
                    border-radius: 4px;
                    font-size: 0.75em;
                    z-index: 10;
                    pointer-events: none;
                    font-weight: 600;
                }

                .netflix-scroll-btn {
                    position: absolute;
                    top: 50%;
                    transform: translateY(-50%);
                    width: 50px;
                    height: 100%;
                    background: rgba(0, 0, 0, 0.6);
                    border: none;
                    color: #fff;
                    font-size: 24px;
                    cursor: pointer;
                    z-index: 200;
                    opacity: 0;
                    transition: opacity 0.3s ease;
                }

                .netflix-row-wrapper:hover .netflix-scroll-btn {
                    opacity: 1;
                }

                .netflix-scroll-btn:hover {
                    background: rgba(0, 0, 0, 0.8);
                }

                .netflix-scroll-btn.left {
                    left: 0;
                }

                .netflix-scroll-btn.right {
                    right: 0;
                }

                .netflix-loading {
                    text-align: center;
                    color: #999;
                    padding: 40px;
                    font-size: 16px;
                }

                /* Mobile responsive */
                @media screen and (max-width: 768px) {
                    .netflix-card {
                        width: 140px;
                        height: 210px;
                    }

                    .netflix-genre-title {
                        font-size: 1.1em;
                    }

                    .netflix-scroll-btn {
                        display: none;
                    }
                }

                /* New Media Notifications - Bottom Left Corner */
                .ratings-notification-container {
                    position: fixed !important;
                    bottom: 20px !important;
                    left: 20px !important;
                    z-index: 9999999 !important;
                    display: flex !important;
                    flex-direction: column !important;
                    gap: 10px !important;
                    max-width: 350px !important;
                    pointer-events: none !important;
                }

                .ratings-notification {
                    background: linear-gradient(135deg, rgba(30, 30, 30, 0.98) 0%, rgba(45, 45, 45, 0.98) 100%) !important;
                    border: 1px solid rgba(255, 255, 255, 0.15) !important;
                    border-left: 4px solid #4CAF50 !important;
                    border-radius: 12px !important;
                    padding: 16px !important;
                    box-shadow: 0 8px 32px rgba(0, 0, 0, 0.4), 0 0 0 1px rgba(255, 255, 255, 0.05) !important;
                    animation: notificationSlideIn 0.4s cubic-bezier(0.4, 0, 0.2, 1) forwards !important;
                    pointer-events: auto !important;
                    display: flex !important;
                    gap: 12px !important;
                    align-items: flex-start !important;
                    backdrop-filter: blur(10px) !important;
                    -webkit-backdrop-filter: blur(10px) !important;
                }

                .ratings-notification.test-notification {
                    border-left-color: #2196F3 !important;
                }

                .ratings-notification.hiding {
                    animation: notificationSlideOut 0.3s cubic-bezier(0.4, 0, 0.2, 1) forwards !important;
                }

                @keyframes notificationSlideIn {
                    from {
                        opacity: 0;
                        transform: translateX(-100%);
                    }
                    to {
                        opacity: 1;
                        transform: translateX(0);
                    }
                }

                @keyframes notificationSlideOut {
                    from {
                        opacity: 1;
                        transform: translateX(0);
                    }
                    to {
                        opacity: 0;
                        transform: translateX(-100%);
                    }
                }

                .ratings-notification-image {
                    width: 50px !important;
                    height: 75px !important;
                    border-radius: 6px !important;
                    object-fit: cover !important;
                    flex-shrink: 0 !important;
                    background: #333 !important;
                }

                .ratings-notification-content {
                    flex: 1 !important;
                    min-width: 0 !important;
                }

                .ratings-notification-header {
                    display: flex !important;
                    align-items: center !important;
                    gap: 6px !important;
                    margin-bottom: 4px !important;
                }

                .ratings-notification-icon {
                    font-size: 14px !important;
                }

                .ratings-notification-label {
                    font-size: 11px !important;
                    font-weight: 600 !important;
                    color: #4CAF50 !important;
                    text-transform: uppercase !important;
                    letter-spacing: 0.5px !important;
                }

                .test-notification .ratings-notification-label {
                    color: #2196F3 !important;
                }

                .ratings-notification-title {
                    font-size: 15px !important;
                    font-weight: 600 !important;
                    color: #fff !important;
                    margin-bottom: 4px !important;
                    overflow: hidden !important;
                    text-overflow: ellipsis !important;
                    white-space: nowrap !important;
                }

                .ratings-notification-meta {
                    font-size: 12px !important;
                    color: #aaa !important;
                }

                .ratings-notification-message {
                    font-size: 13px !important;
                    color: #ccc !important;
                    line-height: 1.4 !important;
                }

                .ratings-notification-close {
                    position: absolute !important;
                    top: 8px !important;
                    right: 8px !important;
                    background: none !important;
                    border: none !important;
                    color: #666 !important;
                    font-size: 18px !important;
                    cursor: pointer !important;
                    padding: 4px !important;
                    line-height: 1 !important;
                    transition: color 0.2s ease !important;
                }

                .ratings-notification-close:hover {
                    color: #fff !important;
                }

                .ratings-notification {
                    position: relative !important;
                }

                /* Admin Test Notification Button */
                #testNotificationBtn {
                    position: absolute !important;
                    top: 8px !important;
                    right: 700px !important;
                    background: rgba(33, 150, 243, 0.9) !important;
                    border: 1px solid rgba(255, 255, 255, 0.2) !important;
                    padding: 10px 20px !important;
                    border-radius: 20px !important;
                    font-size: 13px !important;
                    font-weight: 600 !important;
                    cursor: pointer !important;
                    z-index: 999999 !important;
                    transition: all 0.3s ease !important;
                    color: #fff !important;
                    font-family: "Poppins", -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif !important;
                }

                #testNotificationBtn:hover {
                    background: rgba(33, 150, 243, 1) !important;
                    transform: scale(1.05) !important;
                }

                #testNotificationBtn.hidden {
                    display: none !important;
                }

                @media screen and (max-width: 925px) {
                    #testNotificationBtn {
                        display: none !important;
                    }

                    .ratings-notification-container {
                        left: 10px !important;
                        right: 10px !important;
                        max-width: none !important;
                    }

                    .ratings-notification {
                        padding: 12px !important;
                    }

                    .ratings-notification-image {
                        width: 40px !important;
                        height: 60px !important;
                    }

                    .ratings-notification-title {
                        font-size: 14px !important;
                    }
                }

                /* Latest Media Button - Replaces Sync Play Button */
                #latestMediaBtn {
                    display: flex !important;
                    align-items: center !important;
                    justify-content: center !important;
                    background: transparent !important;
                    border: none !important;
                    cursor: pointer !important;
                    padding: 8px !important;
                    border-radius: 50% !important;
                    transition: background 0.2s ease !important;
                    color: #fff !important;
                    font-size: 24px !important;
                }

                #latestMediaBtn:hover {
                    background: rgba(255, 255, 255, 0.1) !important;
                }

                #latestMediaBtn.hidden {
                    display: none !important;
                }

                #latestMediaBtn svg {
                    width: 24px !important;
                    height: 24px !important;
                    fill: currentColor !important;
                }

                /* Latest Media Badge */
                .latest-media-badge {
                    position: absolute !important;
                    top: 2px !important;
                    right: 2px !important;
                    background: #e91e63 !important;
                    color: #fff !important;
                    font-size: 9px !important;
                    font-weight: 700 !important;
                    min-width: 16px !important;
                    height: 16px !important;
                    border-radius: 8px !important;
                    display: none !important;
                    align-items: center !important;
                    justify-content: center !important;
                    padding: 0 4px !important;
                    line-height: 1 !important;
                    box-shadow: 0 1px 3px rgba(0,0,0,0.3) !important;
                }
                .latest-media-badge.visible {
                    display: flex !important;
                }

                /* Latest Media Dropdown */
                #latestMediaDropdown {
                    position: fixed !important;
                    min-width: 320px !important;
                    max-width: 400px !important;
                    max-height: 70vh !important;
                    overflow-y: auto !important;
                    background: #1a1a1a !important;
                    border: 1px solid rgba(255, 255, 255, 0.15) !important;
                    border-radius: 8px !important;
                    box-shadow: 0 8px 32px rgba(0, 0, 0, 0.8) !important;
                    z-index: 2147483647 !important;
                    display: none;
                }

                #latestMediaDropdown.visible {
                    display: block !important;
                }

                #latestMediaDropdown .latest-header {
                    padding: 12px 14px !important;
                    border-bottom: 1px solid rgba(255, 255, 255, 0.1) !important;
                    font-size: 13px !important;
                    font-weight: 600 !important;
                    color: #fff !important;
                    background: #1a1a1a !important;
                    position: sticky !important;
                    top: 0 !important;
                    z-index: 1 !important;
                }

                #latestMediaDropdown .latest-loading {
                    padding: 20px !important;
                    text-align: center !important;
                    color: #888 !important;
                    font-size: 13px !important;
                }

                #latestMediaDropdown .latest-empty {
                    padding: 20px !important;
                    text-align: center !important;
                    color: #666 !important;
                    font-size: 13px !important;
                }

                #latestMediaDropdown .latest-item {
                    display: flex !important;
                    align-items: center !important;
                    padding: 6px 10px !important;
                    cursor: pointer !important;
                    transition: background 0.15s ease !important;
                    border-bottom: 1px solid rgba(255, 255, 255, 0.03) !important;
                    text-decoration: none !important;
                    gap: 10px !important;
                }

                #latestMediaDropdown .latest-item:hover {
                    background: rgba(255, 255, 255, 0.08) !important;
                }

                #latestMediaDropdown .latest-item:last-child {
                    border-bottom: none !important;
                }

                #latestMediaDropdown .latest-item-image {
                    width: 32px !important;
                    height: 48px !important;
                    object-fit: cover !important;
                    border-radius: 3px !important;
                    background: #2a2a2a !important;
                    flex-shrink: 0 !important;
                }

                #latestMediaDropdown .latest-item-info {
                    flex: 1 !important;
                    min-width: 0 !important;
                    overflow: hidden !important;
                }

                #latestMediaDropdown .latest-item-title {
                    color: #e0e0e0 !important;
                    font-size: 12px !important;
                    font-weight: 500 !important;
                    white-space: nowrap !important;
                    overflow: hidden !important;
                    text-overflow: ellipsis !important;
                    line-height: 1.3 !important;
                }

                #latestMediaDropdown .latest-item-meta {
                    display: flex !important;
                    align-items: center !important;
                    gap: 6px !important;
                    margin-top: 2px !important;
                }

                #latestMediaDropdown .latest-item-year {
                    color: #666 !important;
                    font-size: 10px !important;
                }

                #latestMediaDropdown .latest-item-time {
                    color: #888 !important;
                    font-size: 9px !important;
                    margin-left: auto !important;
                    white-space: nowrap !important;
                }

                #latestMediaDropdown .latest-item-type {
                    padding: 1px 5px !important;
                    border-radius: 3px !important;
                    font-size: 9px !important;
                    font-weight: 600 !important;
                    text-transform: uppercase !important;
                }

                #latestMediaDropdown .latest-item-type.movie {
                    background: rgba(33, 150, 243, 0.25) !important;
                    color: #64b5f6 !important;
                }

                #latestMediaDropdown .latest-item-type.series {
                    background: rgba(76, 175, 80, 0.25) !important;
                    color: #81c784 !important;
                }

                #latestMediaDropdown .latest-item-type.anime {
                    background: rgba(156, 39, 176, 0.25) !important;
                    color: #ba68c8 !important;
                }

                #latestMediaDropdown .latest-item-type.other {
                    background: rgba(158, 158, 158, 0.25) !important;
                    color: #9e9e9e !important;
                }

                /* New episodes badge */
                #latestMediaDropdown .latest-item-badge {
                    display: inline-block !important;
                    margin-left: 6px !important;
                    padding: 2px 6px !important;
                    border-radius: 4px !important;
                    font-size: 9px !important;
                    font-weight: 600 !important;
                    text-transform: uppercase !important;
                    vertical-align: middle !important;
                }
                #latestMediaDropdown .latest-item-badge.new-episodes {
                    background: rgba(0, 200, 83, 0.25) !important;
                    color: #69f0ae !important;
                }
                #latestMediaDropdown .latest-item-badge.is-new {
                    background: rgba(233, 30, 99, 0.35) !important;
                    color: #f48fb1 !important;
                    animation: newBadgePulse 2s ease-in-out infinite !important;
                }
                @keyframes newBadgePulse {
                    0%, 100% { opacity: 1; }
                    50% { opacity: 0.7; }
                }

                /* Scrollbar styling for latest media dropdown */
                #latestMediaDropdown::-webkit-scrollbar {
                    width: 6px !important;
                }

                #latestMediaDropdown::-webkit-scrollbar-track {
                    background: transparent !important;
                }

                #latestMediaDropdown::-webkit-scrollbar-thumb {
                    background: rgba(255, 255, 255, 0.2) !important;
                    border-radius: 3px !important;
                }

                #latestMediaDropdown::-webkit-scrollbar-thumb:hover {
                    background: rgba(255, 255, 255, 0.3) !important;
                }

                @media screen and (max-width: 768px) {
                    #latestMediaDropdown {
                        min-width: 280px !important;
                        max-width: calc(100vw - 20px) !important;
                        right: 10px !important;
                        left: auto !important;
                    }

                    #latestMediaDropdown .latest-item {
                        padding: 5px 8px !important;
                    }

                    #latestMediaDropdown .latest-item-image {
                        width: 28px !important;
                        height: 42px !important;
                    }

                    #latestMediaDropdown .latest-item-title {
                        font-size: 11px !important;
                    }
                }

                /* Media Management Button Styles */
                #mediaManagementBtn {
                    display: flex !important;
                    align-items: center !important;
                    justify-content: center !important;
                    color: rgba(255, 255, 255, 0.8) !important;
                    padding: 0 !important;
                    margin: 0 4px !important;
                    width: 42px !important;
                    height: 42px !important;
                    background: transparent !important;
                    border: none !important;
                    cursor: pointer !important;
                    transition: color 0.2s ease !important;
                    position: relative !important;
                }

                #mediaManagementBtn:hover {
                    color: #fff !important;
                }

                #mediaManagementBtn.hidden {
                    display: none !important;
                }

                #mediaManagementBtn svg {
                    width: 24px !important;
                    height: 24px !important;
                    fill: currentColor !important;
                }

                /* Media Management Modal Styles */
                #mediaManagementModal {
                    display: none;
                    position: fixed;
                    top: 0;
                    left: 0;
                    width: 100%;
                    height: 100%;
                    background: rgba(0, 0, 0, 0.85);
                    z-index: 999999;
                    overflow: auto;
                }

                #mediaManagementModal.show {
                    display: flex;
                    align-items: flex-start;
                    justify-content: center;
                    padding: 20px;
                }

                #mediaManagementModalContent {
                    background: #1a1a1a;
                    border-radius: 12px;
                    width: 100%;
                    max-width: 1200px;
                    max-height: 90vh;
                    overflow: hidden;
                    display: flex;
                    flex-direction: column;
                    position: relative;
                    margin-top: 20px;
                }

                #mediaManagementModalClose {
                    position: absolute;
                    top: 10px;
                    right: 15px;
                    background: transparent;
                    border: none;
                    color: #fff;
                    font-size: 28px;
                    cursor: pointer;
                    z-index: 10;
                    opacity: 0.7;
                    transition: opacity 0.2s;
                }

                #mediaManagementModalClose:hover {
                    opacity: 1;
                }

                #mediaManagementModalTitle {
                    font-size: 20px;
                    font-weight: 600;
                    padding: 15px 20px;
                    border-bottom: 1px solid #333;
                    color: #fff;
                }

                #mediaManagementTabs {
                    display: flex;
                    gap: 0;
                    padding: 0 20px;
                    background: #1a1a1a;
                    border-bottom: 1px solid #333;
                }

                #mediaManagementTabs .media-tab {
                    padding: 12px 24px;
                    background: transparent;
                    border: none;
                    border-bottom: 2px solid transparent;
                    color: #888;
                    font-size: 14px;
                    font-weight: 500;
                    cursor: pointer;
                    transition: all 0.2s ease;
                }

                #mediaManagementTabs .media-tab:hover {
                    color: #fff;
                    background: rgba(255, 255, 255, 0.05);
                }

                #mediaManagementTabs .media-tab.active {
                    color: #52b4e5;
                    border-bottom-color: #52b4e5;
                }

                #mediaManagementControls {
                    display: flex;
                    flex-wrap: wrap;
                    gap: 10px;
                    padding: 15px 20px;
                    background: #222;
                    align-items: center;
                }

                #mediaManagementControls input,
                #mediaManagementControls select {
                    padding: 8px 12px;
                    border: 1px solid #444;
                    border-radius: 6px;
                    background: #333;
                    color: #fff;
                    font-size: 13px;
                }

                #mediaManagementControls input:focus,
                #mediaManagementControls select:focus {
                    outline: none;
                    border-color: #52b4e5;
                }

                #mediaSearchInput {
                    flex: 1;
                    min-width: 200px;
                }

                #mediaManagementBody {
                    flex: 1;
                    overflow-y: auto;
                    padding: 0;
                }

                .media-list-table {
                    width: 100%;
                    border-collapse: collapse;
                }

                .media-list-table th {
                    background: #282828;
                    color: #aaa;
                    font-size: 12px;
                    font-weight: 500;
                    text-align: left;
                    padding: 10px 12px;
                    position: sticky;
                    top: 0;
                    z-index: 5;
                }

                .media-list-table td {
                    padding: 12px;
                    border-bottom: 1px solid #333;
                    color: #ddd;
                    font-size: 13px;
                    vertical-align: middle;
                }

                .media-list-table td.media-actions {
                    vertical-align: middle;
                    text-align: center;
                }

                .media-list-table tr:hover td {
                    background: #282828;
                }

                .media-list-table tbody tr {
                    opacity: 0;
                    transform: translateY(10px);
                    animation: mediaRowFadeIn 0.3s ease forwards;
                }

                @keyframes mediaRowFadeIn {
                    to {
                        opacity: 1;
                        transform: translateY(0);
                    }
                }

                .media-item-image {
                    width: 40px;
                    height: 60px;
                    object-fit: cover;
                    border-radius: 4px;
                    background: #333;
                }

                .media-item-title {
                    font-weight: 500;
                    color: #fff;
                }

                .media-item-title a {
                    color: #52b4e5;
                    text-decoration: none;
                }

                .media-item-title a:hover {
                    text-decoration: underline;
                }

                .media-item-type {
                    font-size: 10px;
                    padding: 2px 6px;
                    border-radius: 3px;
                    background: #2c3e50;
                    color: #fff;
                    display: inline-block;
                    margin-top: 4px;
                }

                .media-item-type.movie {
                    background: #2980b9;
                }

                .media-item-type.series {
                    background: #27ae60;
                }

                .media-item-rating {
                    color: #f1c40f;
                }

                .media-item-scheduled {
                    color: #e74c3c;
                    font-size: 11px;
                    font-weight: 500;
                }

                .scheduled-time-badge {
                    display: inline-block;
                    padding: 4px 10px;
                    border-radius: 4px;
                    color: #fff;
                    font-size: 12px;
                    font-weight: 600;
                }

                .scheduled-actions-wrapper {
                    display: inline-flex;
                    gap: 8px;
                    align-items: center;
                    justify-content: center;
                }

                .scheduled-actions-wrapper .media-action-btn.change {
                    background: linear-gradient(135deg, #3498db, #2980b9);
                    box-shadow: 0 2px 4px rgba(52, 152, 219, 0.3);
                }

                .scheduled-actions-wrapper .media-action-btn.change:hover {
                    background: linear-gradient(135deg, #2980b9, #1f6dad);
                    transform: translateY(-1px);
                    box-shadow: 0 3px 6px rgba(52, 152, 219, 0.4);
                }

                .media-actions {
                    text-align: center;
                    vertical-align: middle;
                }

                .media-action-btn {
                    padding: 8px 14px;
                    border: none;
                    border-radius: 6px;
                    cursor: pointer;
                    font-size: 12px;
                    font-weight: 500;
                    transition: all 0.2s ease;
                    white-space: nowrap;
                }

                .media-action-btn.delete {
                    background: linear-gradient(135deg, #e74c3c, #c0392b);
                    color: #fff;
                    box-shadow: 0 2px 4px rgba(231, 76, 60, 0.3);
                }

                .media-action-btn.delete:hover {
                    background: linear-gradient(135deg, #c0392b, #a93226);
                    transform: translateY(-1px);
                    box-shadow: 0 3px 6px rgba(231, 76, 60, 0.4);
                }

                .media-action-btn.cancel {
                    background: linear-gradient(135deg, #95a5a6, #7f8c8d);
                    color: #fff;
                    box-shadow: 0 2px 4px rgba(149, 165, 166, 0.3);
                }

                .media-action-btn.cancel:hover {
                    background: linear-gradient(135deg, #7f8c8d, #6c7a7b);
                    transform: translateY(-1px);
                    box-shadow: 0 3px 6px rgba(149, 165, 166, 0.4);
                }

                #mediaManagementPagination {
                    display: flex;
                    justify-content: center;
                    align-items: center;
                    padding: 15px;
                    background: #222;
                    border-top: 1px solid #333;
                }

                .pagination-wrapper {
                    display: flex;
                    align-items: center;
                    gap: 15px;
                }

                .pagination-nav-btn {
                    display: flex;
                    align-items: center;
                    gap: 6px;
                    padding: 8px 16px;
                    border: none;
                    border-radius: 6px;
                    background: linear-gradient(135deg, #3a3a3a 0%, #2a2a2a 100%);
                    color: #fff;
                    cursor: pointer;
                    font-size: 13px;
                    font-weight: 500;
                    transition: all 0.2s ease;
                }

                .pagination-nav-btn:hover:not(:disabled) {
                    background: linear-gradient(135deg, #4a4a4a 0%, #3a3a3a 100%);
                    transform: translateY(-1px);
                }

                .pagination-nav-btn:disabled {
                    opacity: 0.4;
                    cursor: not-allowed;
                    transform: none;
                }

                .pagination-arrow {
                    font-size: 16px;
                    font-weight: bold;
                }

                .pagination-center {
                    display: flex;
                    align-items: center;
                    gap: 8px;
                    padding: 6px 12px;
                    background: #2a2a2a;
                    border-radius: 6px;
                }

                .pagination-label {
                    color: #888;
                    font-size: 13px;
                }

                #mediaPageInput {
                    width: 55px;
                    padding: 6px 8px;
                    border: 1px solid #444;
                    border-radius: 4px;
                    background: #1a1a1a;
                    color: #fff;
                    text-align: center;
                    font-size: 14px;
                    font-weight: 500;
                }

                #mediaPageInput:focus {
                    outline: none;
                    border-color: #00a4dc;
                }

                .pagination-go-btn {
                    padding: 6px 12px;
                    border: none;
                    border-radius: 4px;
                    background: #00a4dc;
                    color: #fff;
                    cursor: pointer;
                    font-size: 12px;
                    font-weight: 600;
                    transition: all 0.2s ease;
                }

                .pagination-go-btn:hover {
                    background: #0095c8;
                }

                .pagination-info {
                    color: #888;
                    font-size: 13px;
                }

                .pagination-items {
                    color: #666;
                    font-size: 12px;
                }

                /* Settings Tab */
                .media-settings-tab {
                    font-size: 16px !important;
                    padding: 8px 12px !important;
                }

                #mediaManagementSettings {
                    padding: 20px;
                    background: #1e1e1e;
                    min-height: 300px;
                }

                #mediaManagementSettings .settings-section {
                    background: #252525;
                    border-radius: 8px;
                    padding: 20px;
                    margin-bottom: 15px;
                }

                #mediaManagementSettings h3 {
                    margin: 0 0 10px 0;
                    color: #fff;
                    font-size: 16px;
                    font-weight: 500;
                }

                #mediaTypeCheckboxes {
                    display: flex;
                    flex-wrap: wrap;
                    gap: 10px;
                    margin-top: 15px;
                }

                .media-type-checkbox {
                    display: flex;
                    align-items: center;
                    gap: 8px;
                    padding: 10px 15px;
                    background: #333;
                    border-radius: 6px;
                    cursor: pointer;
                    transition: all 0.2s ease;
                }

                .media-type-checkbox:hover {
                    background: #3a3a3a;
                }

                .media-type-checkbox input {
                    width: 18px;
                    height: 18px;
                    cursor: pointer;
                }

                .media-type-checkbox label {
                    color: #fff;
                    font-size: 14px;
                    cursor: pointer;
                }

                .media-type-checkbox.checked {
                    background: rgba(0, 164, 220, 0.2);
                    border: 1px solid #00a4dc;
                }

                /* Deletion Dialog */
                #deletionDialog {
                    display: none;
                    position: fixed;
                    top: 0;
                    left: 0;
                    width: 100%;
                    height: 100%;
                    background: rgba(0, 0, 0, 0.7);
                    z-index: 9999999;
                    align-items: center;
                    justify-content: center;
                }

                #deletionDialog.show {
                    display: flex;
                }

                #deletionDialogContent {
                    background: #1a1a1a;
                    border-radius: 12px;
                    padding: 20px;
                    max-width: 400px;
                    width: 90%;
                }

                #deletionDialogTitle {
                    font-size: 16px;
                    font-weight: 600;
                    margin-bottom: 15px;
                    color: #fff;
                }

                #deletionDialogOptions {
                    display: flex;
                    flex-wrap: wrap;
                    gap: 10px;
                }

                #deletionDialogOptions button {
                    flex: 1;
                    min-width: 80px;
                    padding: 10px 15px;
                    border: none;
                    border-radius: 6px;
                    cursor: pointer;
                    font-size: 13px;
                    transition: all 0.2s;
                }

                .deletion-option-btn {
                    background: #e74c3c;
                    color: #fff;
                }

                .deletion-option-btn:hover {
                    background: #c0392b;
                }

                .deletion-cancel-btn {
                    background: #555;
                    color: #fff;
                    width: 100%;
                    margin-top: 10px;
                }

                .deletion-cancel-btn:hover {
                    background: #666;
                }

                .deletion-close-btn {
                    position: absolute;
                    top: 10px;
                    right: 10px;
                    background: transparent;
                    border: none;
                    color: #888;
                    font-size: 24px;
                    cursor: pointer;
                    padding: 0;
                    width: 30px;
                    height: 30px;
                    line-height: 30px;
                }

                .deletion-close-btn:hover {
                    color: #fff;
                }

                #deletionDialogContent {
                    position: relative;
                }

                #deletionDialogCustom {
                    display: flex;
                    gap: 10px;
                    margin-top: 15px;
                    padding-top: 15px;
                    border-top: 1px solid #333;
                }

                #deletionCustomHours {
                    flex: 1;
                    padding: 10px;
                    border: 1px solid #444;
                    border-radius: 6px;
                    background: #2a2a2a;
                    color: #fff;
                    font-size: 14px;
                }

                #deletionCustomHours::placeholder {
                    color: #666;
                }

                .deletion-custom-btn {
                    padding: 10px 20px;
                    background: #00a4dc;
                    color: #fff;
                    border: none;
                    border-radius: 6px;
                    cursor: pointer;
                    font-size: 14px;
                }

                .deletion-custom-btn:hover {
                    background: #0095c8;
                }

                .detail-leaving-badge {
                    display: inline-block !important;
                    background: #e74c3c !important;
                    color: #fff !important;
                    font-size: 12px !important;
                    font-weight: 600 !important;
                    padding: 4px 10px !important;
                    border-radius: 4px !important;
                    margin-right: 10px !important;
                }

                /* Media Management responsive */
                @media (max-width: 768px) {
                    #mediaManagementModalContent {
                        max-height: 95vh;
                        margin-top: 10px;
                    }

                    #mediaManagementControls {
                        flex-direction: column;
                    }

                    #mediaSearchInput {
                        width: 100%;
                    }

                    .media-list-table th,
                    .media-list-table td {
                        padding: 6px 8px;
                        font-size: 11px;
                    }

                    .media-item-image {
                        width: 30px;
                        height: 45px;
                    }
                }

                /* ============ LIVE CHAT STYLES ============ */

                /* Chat Button - replaces Cast button position */
                #chatBtn {
                    background: transparent !important;
                    border: none !important;
                    cursor: pointer !important;
                    padding: 8px !important;
                    display: flex !important;
                    align-items: center !important;
                    justify-content: center !important;
                    position: relative !important;
                    transition: opacity 0.2s ease !important;
                }

                #chatBtn:hover {
                    opacity: 0.7 !important;
                }

                #chatBtn.hidden {
                    display: none !important;
                }

                #chatBtnIcon {
                    font-size: 24px !important;
                }

                /* Chat notification badge */
                .chat-badge {
                    position: absolute !important;
                    top: 2px !important;
                    right: 2px !important;
                    background: #ff4444 !important;
                    color: white !important;
                    border-radius: 50% !important;
                    min-width: 18px !important;
                    height: 18px !important;
                    display: flex !important;
                    align-items: center !important;
                    justify-content: center !important;
                    font-size: 10px !important;
                    font-weight: 700 !important;
                    border: 2px solid #1e1e1e !important;
                    animation: badgePulse 1.5s ease-in-out infinite !important;
                    padding: 0 4px !important;
                }

                .chat-badge.hidden {
                    display: none !important;
                }

                /* Chat Window Container */
                #chatWindow {
                    position: fixed !important;
                    bottom: 80px !important;
                    right: 20px !important;
                    width: 380px !important;
                    height: 550px !important;
                    background: #1a1a1a !important;
                    border: 1px solid rgba(255, 255, 255, 0.15) !important;
                    border-radius: 12px !important;
                    box-shadow: 0 8px 32px rgba(0, 0, 0, 0.6) !important;
                    z-index: 999999 !important;
                    display: none;
                    flex-direction: column !important;
                    overflow: hidden !important;
                }

                #chatWindow.visible {
                    display: flex !important;
                }

                /* Chat Header */
                .chat-header {
                    display: flex !important;
                    align-items: center !important;
                    justify-content: space-between !important;
                    padding: 12px 16px !important;
                    background: #252525 !important;
                    border-bottom: 1px solid rgba(255, 255, 255, 0.1) !important;
                }

                .chat-header-left {
                    display: flex !important;
                    align-items: center !important;
                    gap: 10px !important;
                }

                .chat-header-title {
                    font-size: 16px !important;
                    font-weight: 600 !important;
                    color: #fff !important;
                }

                .chat-online-count {
                    font-size: 12px !important;
                    color: #4CAF50 !important;
                    display: flex !important;
                    align-items: center !important;
                    gap: 4px !important;
                }

                .chat-online-dot {
                    width: 8px !important;
                    height: 8px !important;
                    border-radius: 50% !important;
                    background: #4CAF50 !important;
                }

                .chat-header-right {
                    display: flex !important;
                    align-items: center !important;
                    gap: 8px !important;
                }

                .chat-header-btn {
                    background: transparent !important;
                    border: none !important;
                    color: #888 !important;
                    cursor: pointer !important;
                    padding: 4px !important;
                    font-size: 18px !important;
                    transition: color 0.2s ease !important;
                }

                .chat-header-btn:hover {
                    color: #fff !important;
                }

                /* Chat Messages Container */
                .chat-messages {
                    flex: 1 !important;
                    overflow-y: auto !important;
                    padding: 12px !important;
                    display: flex !important;
                    flex-direction: column !important;
                    gap: 8px !important;
                }

                .chat-messages::-webkit-scrollbar {
                    width: 6px !important;
                }

                .chat-messages::-webkit-scrollbar-track {
                    background: transparent !important;
                }

                .chat-messages::-webkit-scrollbar-thumb {
                    background: rgba(255, 255, 255, 0.2) !important;
                    border-radius: 3px !important;
                }

                /* Chat Message */
                .chat-message {
                    display: flex !important;
                    gap: 10px !important;
                    padding: 8px !important;
                    border-radius: 8px !important;
                    transition: background 0.2s ease !important;
                }

                .chat-message:hover {
                    background: rgba(255, 255, 255, 0.05) !important;
                }

                .chat-message.own {
                    flex-direction: row-reverse !important;
                }

                .chat-avatar {
                    width: 36px !important;
                    height: 36px !important;
                    border-radius: 50% !important;
                    background: #333 !important;
                    display: flex !important;
                    align-items: center !important;
                    justify-content: center !important;
                    font-size: 14px !important;
                    font-weight: 600 !important;
                    color: #fff !important;
                    flex-shrink: 0 !important;
                    overflow: hidden !important;
                }

                .chat-avatar img {
                    width: 100% !important;
                    height: 100% !important;
                    object-fit: cover !important;
                }

                .chat-message-content {
                    flex: 1 !important;
                    min-width: 0 !important;
                }

                .chat-message-header {
                    display: flex !important;
                    align-items: center !important;
                    gap: 8px !important;
                    margin-bottom: 4px !important;
                }

                .chat-message.own .chat-message-header {
                    flex-direction: row-reverse !important;
                }

                .chat-username {
                    font-size: 13px !important;
                    font-weight: 600 !important;
                    color: #00a4dc !important;
                }

                .chat-username.moderator {
                    color: #4CAF50 !important;
                }

                .chat-username.admin {
                    color: #ff9800 !important;
                }

                .chat-timestamp {
                    font-size: 11px !important;
                    color: #666 !important;
                }

                .chat-message-text {
                    font-size: 14px !important;
                    color: #ddd !important;
                    line-height: 1.4 !important;
                    word-wrap: break-word !important;
                }

                .chat-message.own .chat-message-text {
                    text-align: right !important;
                }

                .chat-message-gif {
                    max-width: 200px !important;
                    border-radius: 8px !important;
                    margin-top: 4px !important;
                }

                .chat-message-deleted {
                    color: #666 !important;
                    font-style: italic !important;
                }

                /* Chat Message Actions (for mods/admins) */
                .chat-message-actions {
                    display: none;
                    gap: 4px !important;
                    margin-top: 4px !important;
                }

                .chat-message:hover .chat-message-actions {
                    display: flex !important;
                }

                .chat-message.own .chat-message-actions {
                    justify-content: flex-end !important;
                }

                .chat-action-btn {
                    background: transparent !important;
                    border: none !important;
                    color: #666 !important;
                    cursor: pointer !important;
                    padding: 2px 6px !important;
                    font-size: 11px !important;
                    border-radius: 4px !important;
                    transition: all 0.2s ease !important;
                }

                .chat-action-btn:hover {
                    background: rgba(255, 255, 255, 0.1) !important;
                    color: #fff !important;
                }

                .chat-action-btn.delete:hover {
                    background: rgba(255, 68, 68, 0.2) !important;
                    color: #ff4444 !important;
                }

                /* Typing indicator */
                .chat-typing {
                    padding: 8px 12px !important;
                    font-size: 12px !important;
                    color: #888 !important;
                    font-style: italic !important;
                    display: none;
                }

                .chat-typing.visible {
                    display: block !important;
                }

                /* Chat Input Area */
                .chat-input-area {
                    padding: 12px !important;
                    background: #252525 !important;
                    border-top: 1px solid rgba(255, 255, 255, 0.1) !important;
                }

                .chat-input-row {
                    display: flex !important;
                    align-items: flex-end !important;
                    gap: 8px !important;
                }

                .chat-input-wrapper {
                    flex: 1 !important;
                    position: relative !important;
                    min-width: 0 !important;
                }

                .chat-input {
                    width: 100% !important;
                    box-sizing: border-box !important;
                    background: #333 !important;
                    border: 1px solid rgba(255, 255, 255, 0.1) !important;
                    border-radius: 20px !important;
                    padding: 10px 70px 10px 16px !important;
                    color: #fff !important;
                    font-size: 14px !important;
                    resize: none !important;
                    min-height: 40px !important;
                    max-height: 100px !important;
                    outline: none !important;
                    overflow-y: auto !important;
                }

                .chat-input:focus {
                    border-color: rgba(0, 164, 220, 0.5) !important;
                }

                .chat-input::placeholder {
                    color: #666 !important;
                }

                .chat-input-btns {
                    position: absolute !important;
                    right: 8px !important;
                    bottom: 6px !important;
                    display: flex !important;
                    gap: 4px !important;
                }

                .chat-emoji-btn,
                .chat-gif-btn {
                    background: transparent !important;
                    border: none !important;
                    cursor: pointer !important;
                    padding: 4px !important;
                    font-size: 18px !important;
                    opacity: 0.6 !important;
                    transition: opacity 0.2s ease !important;
                }

                .chat-emoji-btn:hover,
                .chat-gif-btn:hover {
                    opacity: 1 !important;
                }

                .chat-send-btn {
                    background: #00a4dc !important;
                    border: none !important;
                    border-radius: 50% !important;
                    width: 40px !important;
                    height: 40px !important;
                    cursor: pointer !important;
                    display: flex !important;
                    align-items: center !important;
                    justify-content: center !important;
                    color: #fff !important;
                    font-size: 18px !important;
                    transition: background 0.2s ease !important;
                }

                .chat-send-btn:hover {
                    background: #0095c8 !important;
                }

                .chat-send-btn:disabled {
                    background: #333 !important;
                    cursor: not-allowed !important;
                }

                /* Emoji Picker */
                .chat-emoji-picker {
                    position: absolute !important;
                    bottom: 100% !important;
                    right: 0 !important;
                    width: 320px !important;
                    max-height: 300px !important;
                    background: #252525 !important;
                    border: 1px solid rgba(255, 255, 255, 0.15) !important;
                    border-radius: 8px !important;
                    box-shadow: 0 4px 20px rgba(0, 0, 0, 0.5) !important;
                    margin-bottom: 8px !important;
                    display: none;
                    flex-direction: column !important;
                    overflow: hidden !important;
                }

                .chat-emoji-picker.visible {
                    display: flex !important;
                }

                .chat-emoji-categories {
                    display: flex !important;
                    border-bottom: 1px solid rgba(255, 255, 255, 0.1) !important;
                    padding: 4px !important;
                }

                .chat-emoji-category-btn {
                    flex: 1 !important;
                    background: transparent !important;
                    border: none !important;
                    padding: 8px !important;
                    font-size: 16px !important;
                    cursor: pointer !important;
                    opacity: 0.5 !important;
                    transition: opacity 0.2s ease !important;
                }

                .chat-emoji-category-btn:hover,
                .chat-emoji-category-btn.active {
                    opacity: 1 !important;
                }

                .chat-emoji-list {
                    flex: 1 !important;
                    overflow-y: auto !important;
                    padding: 8px !important;
                    display: grid !important;
                    grid-template-columns: repeat(8, 1fr) !important;
                    gap: 4px !important;
                }

                .chat-emoji-item {
                    background: transparent !important;
                    border: none !important;
                    padding: 6px !important;
                    font-size: 20px !important;
                    cursor: pointer !important;
                    border-radius: 4px !important;
                    transition: background 0.2s ease !important;
                }

                .chat-emoji-item:hover {
                    background: rgba(255, 255, 255, 0.1) !important;
                }

                /* GIF Picker */
                .chat-gif-picker {
                    position: absolute !important;
                    bottom: 100% !important;
                    right: 0 !important;
                    width: 320px !important;
                    max-height: 350px !important;
                    background: #252525 !important;
                    border: 1px solid rgba(255, 255, 255, 0.15) !important;
                    border-radius: 8px !important;
                    box-shadow: 0 4px 20px rgba(0, 0, 0, 0.5) !important;
                    margin-bottom: 8px !important;
                    display: none;
                    flex-direction: column !important;
                    overflow: hidden !important;
                }

                .chat-gif-picker.visible {
                    display: flex !important;
                }

                .chat-gif-search {
                    padding: 8px !important;
                    border-bottom: 1px solid rgba(255, 255, 255, 0.1) !important;
                }

                .chat-gif-search input {
                    width: 100% !important;
                    background: #333 !important;
                    border: none !important;
                    border-radius: 4px !important;
                    padding: 8px 12px !important;
                    color: #fff !important;
                    font-size: 13px !important;
                    outline: none !important;
                }

                .chat-gif-list {
                    flex: 1 !important;
                    overflow-y: auto !important;
                    padding: 8px !important;
                    display: grid !important;
                    grid-template-columns: repeat(2, 1fr) !important;
                    gap: 8px !important;
                }

                .chat-gif-item {
                    cursor: pointer !important;
                    border-radius: 4px !important;
                    overflow: hidden !important;
                    aspect-ratio: 16/9 !important;
                }

                .chat-gif-item img {
                    width: 100% !important;
                    height: 100% !important;
                    object-fit: cover !important;
                }

                .chat-gif-powered {
                    padding: 6px !important;
                    text-align: center !important;
                    font-size: 10px !important;
                    color: #666 !important;
                    border-top: 1px solid rgba(255, 255, 255, 0.1) !important;
                }

                /* Chat Settings/Admin Panel */
                .chat-admin-panel {
                    position: absolute !important;
                    top: 100% !important;
                    right: 0 !important;
                    width: 280px !important;
                    background: #252525 !important;
                    border: 1px solid rgba(255, 255, 255, 0.15) !important;
                    border-radius: 8px !important;
                    box-shadow: 0 4px 20px rgba(0, 0, 0, 0.5) !important;
                    margin-top: 4px !important;
                    display: none;
                    z-index: 10 !important;
                }

                .chat-admin-panel.visible {
                    display: block !important;
                }

                .chat-admin-section {
                    padding: 12px !important;
                    border-bottom: 1px solid rgba(255, 255, 255, 0.1) !important;
                }

                .chat-admin-section:last-child {
                    border-bottom: none !important;
                }

                .chat-admin-title {
                    font-size: 12px !important;
                    font-weight: 600 !important;
                    color: #888 !important;
                    text-transform: uppercase !important;
                    margin-bottom: 8px !important;
                }

                .chat-admin-list {
                    max-height: 150px !important;
                    overflow-y: auto !important;
                }

                .chat-admin-item {
                    display: flex !important;
                    align-items: center !important;
                    justify-content: space-between !important;
                    padding: 6px 0 !important;
                }

                .chat-admin-user {
                    display: flex !important;
                    align-items: center !important;
                    gap: 8px !important;
                    font-size: 13px !important;
                    color: #fff !important;
                }

                .chat-admin-btn {
                    background: transparent !important;
                    border: 1px solid rgba(255, 255, 255, 0.2) !important;
                    color: #888 !important;
                    padding: 4px 8px !important;
                    font-size: 11px !important;
                    border-radius: 4px !important;
                    cursor: pointer !important;
                    transition: all 0.2s ease !important;
                }

                .chat-admin-btn:hover {
                    border-color: rgba(255, 255, 255, 0.4) !important;
                    color: #fff !important;
                }

                .chat-admin-btn.danger:hover {
                    border-color: #ff4444 !important;
                    color: #ff4444 !important;
                }

                /* Chat Status Messages */
                .chat-status {
                    text-align: center !important;
                    padding: 20px !important;
                    color: #888 !important;
                    font-size: 13px !important;
                }

                .chat-status.error {
                    color: #ff4444 !important;
                }

                .chat-status.warning {
                    color: #ff9800 !important;
                }

                /* Chat Empty State */
                .chat-empty {
                    flex: 1 !important;
                    display: flex !important;
                    flex-direction: column !important;
                    align-items: center !important;
                    justify-content: center !important;
                    color: #666 !important;
                    padding: 20px !important;
                    text-align: center !important;
                }

                .chat-empty-icon {
                    font-size: 48px !important;
                    margin-bottom: 12px !important;
                    opacity: 0.5 !important;
                }

                /* Chat Mobile Responsive */
                @media (max-width: 480px) {
                    #chatWindow {
                        position: fixed !important;
                        top: 0 !important;
                        left: 0 !important;
                        right: 0 !important;
                        bottom: 0 !important;
                        width: 100% !important;
                        height: 100% !important;
                        border-radius: 0 !important;
                        z-index: 9999999 !important;
                    }

                    .chat-emoji-picker,
                    .chat-gif-picker {
                        width: 100% !important;
                        left: 0 !important;
                        right: 0 !important;
                        border-radius: 0 !important;
                        max-height: 50vh !important;
                    }

                    .chat-admin-panel {
                        width: 100% !important;
                        left: 0 !important;
                        right: 0 !important;
                    }
                }
            `;

            const styleSheet = document.createElement('style');
            styleSheet.id = 'ratingsPluginStyles';
            styleSheet.textContent = styles;
            document.head.appendChild(styleSheet);
        },

        /**
         * Observe detail pages for item changes
         */
        observeDetailPages: function () {
            const self = this;
            let lastUrl = location.href;
            let lastItemId = null;
            let checkTimer = null;

            const checkForPageChange = function () {
                // Debounce rapid checks
                if (checkTimer) {
                    clearTimeout(checkTimer);
                }

                checkTimer = setTimeout(() => {
                    const url = location.href;
                    const itemId = self.getItemIdFromUrl();

                    // Only trigger if URL changed or item ID changed
                    if (url !== lastUrl || itemId !== lastItemId) {
                        lastUrl = url;
                        lastItemId = itemId;

                        // Remove old component if it exists
                        const oldComponent = document.getElementById('ratingsPluginComponent');
                        if (oldComponent) {
                            oldComponent.remove();
                        }

                        self.onPageChange();
                    }
                }, 100); // Small debounce to prevent multiple rapid fires
            };

            // Listen for hash changes (Jellyfin uses hash-based routing)
            window.addEventListener('hashchange', checkForPageChange);

            // Listen for popstate (back/forward navigation)
            window.addEventListener('popstate', checkForPageChange);

            // Use setInterval as more aggressive polling for SPA navigation detection
            setInterval(() => {
                const url = location.href;
                const itemId = self.getItemIdFromUrl();

                if (url !== lastUrl || itemId !== lastItemId) {
                    lastUrl = url;
                    lastItemId = itemId;

                    // Remove old component if it exists
                    const oldComponent = document.getElementById('ratingsPluginComponent');
                    if (oldComponent) {
                        oldComponent.remove();
                    }

                    self.onPageChange();
                }
            }, 500); // Check every 500ms for URL changes

            // Initial check
            this.onPageChange();
        },

        /**
         * Handle page change
         */
        onPageChange: function () {
            if (!this.ratingsEnabled) return;
            const itemId = this.getItemIdFromUrl();
            if (itemId) {
                this.waitForElementAndInject(itemId);
            }
        },

        /**
         * Wait for page elements to load before injecting
         */
        waitForElementAndInject: function (itemId) {
            const self = this;
            let attempts = 0;
            const maxAttempts = 100; // Try for ~10 seconds max

            const checkInterval = setInterval(() => {
                attempts++;

                // Check if the detailRibbon element exists (primary target)
                const detailRibbon = document.querySelector('.detailRibbon');

                if (detailRibbon) {
                    clearInterval(checkInterval);
                    self.injectRatingComponent(itemId);
                } else if (attempts >= maxAttempts) {
                    // Give up after max attempts
                    clearInterval(checkInterval);
                }
            }, 100); // Check every 100ms for faster detection
        },

        /**
         * Get item ID from URL
         */
        getItemIdFromUrl: function () {
            // Try multiple URL patterns
            const url = window.location.href;
            const pathname = window.location.pathname;
            const hash = window.location.hash;
            const search = window.location.search;


            // Pattern 1: Hash-based routing (#!/details?id=...)
            let match = hash.match(/[?&]id=([a-f0-9]+)/i);
            if (match) {
                return match[1];
            }

            // Pattern 2: Query string (?id=...)
            match = search.match(/[?&]id=([a-f0-9]+)/i);
            if (match) {
                return match[1];
            }

            // Pattern 3: Path-based (/item/id or /details/id)
            match = pathname.match(/\/(?:item|details)\/([a-f0-9]+)/i);
            if (match) {
                return match[1];
            }

            // Pattern 4: Anywhere in URL
            match = url.match(/id=([a-f0-9]{32})/i);
            if (match) {
                return match[1];
            }

            return null;
        },

        /**
         * Inject rating component into the page
         */
        injectRatingComponent: function (itemId) {
            if (document.getElementById('ratingsPluginComponent')) {
                return; // Already injected
            }

            const container = document.createElement('div');
            container.id = 'ratingsPluginComponent';
            container.className = 'ratings-plugin-container';

            container.innerHTML = `
                <div class="ratings-plugin-stars" id="ratingsPluginStars">
                    ${this.generateStars()}
                    <div class="ratings-plugin-popup" id="ratingsPluginPopup">
                        <div class="ratings-plugin-popup-title">User Ratings</div>
                        <ul class="ratings-plugin-popup-list" id="ratingsPluginPopupList">
                            <li class="ratings-plugin-popup-empty">Loading...</li>
                        </ul>
                    </div>
                </div>
                <div class="ratings-plugin-stats" id="ratingsPluginStats">
                    <span class="ratings-plugin-loading">Loading ratings...</span>
                </div>
            `;

            // Position above .detailRibbon vertically, aligned with .detailSection horizontally
            const detailRibbon = document.querySelector('.detailRibbon');
            const detailSection = document.querySelector('.detailSection');

            if (detailRibbon) {
                detailRibbon.classList.add('ratings-plugin-ribbon-anchor');
                detailRibbon.insertBefore(container, detailRibbon.firstChild);

                // Align widget: on desktop align with detailSection, on mobile CSS handles centering
                function alignWidget() {
                    var el = document.getElementById('ratingsPluginComponent');
                    var ribbon = document.querySelector('.detailRibbon');
                    if (!el || !ribbon) return;

                    if (window.innerWidth <= 1024) {
                        // Mobile/tablet: CSS handles centering via left:60% + translateX(-50%)
                        el.style.left = '';
                        return;
                    }

                    var section = document.querySelector('.detailSection');
                    if (section) {
                        var rr = ribbon.getBoundingClientRect();
                        var sr = section.getBoundingClientRect();
                        el.style.left = (sr.left - rr.left) + 'px';
                    }
                }

                alignWidget();

                var realignTimer = null;
                window.addEventListener('resize', function () {
                    if (realignTimer) clearTimeout(realignTimer);
                    realignTimer = setTimeout(alignWidget, 100);
                });
            } else {
                // Fallback
                const detailPageContent = document.querySelector('.detailPageContent') ||
                                         document.querySelector('.itemDetailPage') ||
                                         document.querySelector('.detailPage-content');
                if (detailPageContent) {
                    detailPageContent.insertBefore(container, detailPageContent.firstChild);
                }
            }

            this.attachEventListeners(itemId);
            this.loadRatings(itemId);

            // Apply badge display profile after widget is injected
            this.applyBadgeProfile();
        },

        /**
         * Generate star HTML
         */
        generateStars: function () {
            let html = '';
            for (let i = 1; i <= 10; i++) {
                html += `<span class="ratings-plugin-star" data-rating="${i}">★</span>`;
            }
            return html;
        },

        /**
         * Attach event listeners
         */
        attachEventListeners: function (itemId) {
            const self = this;
            const stars = document.querySelectorAll('.ratings-plugin-star');
            const popup = document.getElementById('ratingsPluginPopup');
            const starsContainer = document.getElementById('ratingsPluginStars');

            stars.forEach(star => {
                star.addEventListener('click', () => {
                    const rating = parseInt(star.getAttribute('data-rating'));
                    // Check if clicking the same rating - toggle off
                    if (self.currentUserRating === rating) {
                        self.deleteRating(itemId);
                    } else {
                        self.submitRating(itemId, rating);
                    }
                });

                star.addEventListener('mouseenter', () => {
                    const rating = parseInt(star.getAttribute('data-rating'));
                    this.highlightStars(rating);
                });
            });

            starsContainer.addEventListener('mouseleave', () => {
                this.loadRatings(itemId); // Refresh to show actual rating
            });

            // Show popup on hover over stars container
            starsContainer.addEventListener('mouseenter', () => {
                this.showDetailedRatings(itemId);
            });

            starsContainer.addEventListener('mouseleave', () => {
                popup.classList.remove('visible');
            });
        },

        /**
         * Highlight stars up to rating
         */
        highlightStars: function (rating) {
            const stars = document.querySelectorAll('.ratings-plugin-star');
            stars.forEach((star, index) => {
                if (index < rating) {
                    star.classList.add('hover');
                } else {
                    star.classList.remove('hover');
                }
            });
        },

        /**
         * Load ratings for item
         */
        loadRatings: function (itemId) {
            const self = this;
            const statsElement = document.getElementById('ratingsPluginStats');


            // Build URL with authentication
            const baseUrl = ApiClient.serverAddress();
            const accessToken = ApiClient.accessToken();
            const url = `${baseUrl}/Ratings/Items/${itemId}/Stats`;

            // Get deviceId from localStorage or generate one
            let deviceId = localStorage.getItem('_deviceId2');
            if (!deviceId) {
                deviceId = 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function(c) {
                    const r = Math.random() * 16 | 0;
                    const v = c === 'x' ? r : (r & 0x3 | 0x8);
                    return v.toString(16);
                });
                localStorage.setItem('_deviceId2', deviceId);
            }

            // Build proper X-Emby-Authorization header
            const authHeader = `MediaBrowser Client="Jellyfin Web", Device="Browser", DeviceId="${deviceId}", Version="10.11.0", Token="${accessToken}"`;


            fetch(url, {
                method: 'GET',
                credentials: 'include',
                headers: {
                    'Content-Type': 'application/json',
                    'X-Emby-Authorization': authHeader
                }
            })
                .then(response => {
                    if (!response.ok) {
                        throw new Error(`HTTP error! status: ${response.status}`);
                    }
                    return response.json();
                })
                .then(stats => {
                    // Track user's current rating for toggle-off feature
                    self.currentUserRating = stats.UserRating || 0;
                    self.updateStarDisplay(stats.UserRating || 0);

                    let statsHtml = '';
                    if (stats.TotalRatings > 0) {
                        statsHtml = `<span class="ratings-plugin-average">${stats.AverageRating.toFixed(1)}/10</span> - ${stats.TotalRatings} rating${stats.TotalRatings !== 1 ? 's' : ''}`;
                        if (stats.UserRating) {
                            statsHtml += `<div class="ratings-plugin-your-rating">Your rating: ${stats.UserRating}/10 <span class="ratings-plugin-remove-hint">(click to remove)</span></div>`;
                        }
                    } else {
                        statsHtml = 'No ratings yet. Be the first to rate!';
                    }

                    if (statsElement) {
                        statsElement.innerHTML = statsHtml;
                    }
                })
                .catch(err => {
                    if (statsElement) {
                        statsElement.innerHTML = 'Error loading ratings';
                    }
                });
        },

        /**
         * Update star display
         */
        updateStarDisplay: function (rating) {
            const stars = document.querySelectorAll('.ratings-plugin-star');

            stars.forEach((star, index) => {
                star.classList.remove('filled', 'hover');
                if (index < rating) {
                    star.classList.add('filled');
                }
            });
        },

        /**
         * Submit rating
         */
        submitRating: function (itemId, rating) {
            const self = this;


            if (!window.ApiClient) {
                return;
            }

            // Gather all authentication info
            const baseUrl = ApiClient.serverAddress();
            const accessToken = ApiClient.accessToken();
            const deviceId = ApiClient.deviceId();
            const url = `${baseUrl}/Ratings/Items/${itemId}/Rating?rating=${rating}`;


            // Build proper X-Emby-Authorization header (Jellyfin's dedicated auth header)
            const authHeader = `MediaBrowser Client="Jellyfin Web", Device="Browser", DeviceId="${deviceId}", Version="10.11.0", Token="${accessToken}"`;

            const requestOptions = {
                method: 'POST',
                credentials: 'include',
                headers: {
                    'Content-Type': 'application/json',
                    'X-Emby-Authorization': authHeader
                }
            };

            fetch(url, requestOptions)
                .then(function(response) {

                    if (!response.ok) {
                        return response.text().then(function(errorText) {
                            throw new Error('HTTP ' + response.status + ': ' + errorText);
                        });
                    }
                    return response.text().then(function(text) {
                        return text ? JSON.parse(text) : {};
                    });
                })
                .then(function(data) {

                    // Update current rating for toggle-off feature
                    self.currentUserRating = rating;

                    // Immediately update the star display for instant feedback
                    self.updateStarDisplay(rating);

                    // Then reload full stats from server
                    self.loadRatings(itemId);

                    if (window.require) {
                        require(['toast'], function(toast) {
                            toast('Rated ' + rating + '/10');
                        });
                    }
                })
                .catch(function(err) {

                    if (window.require) {
                        require(['toast'], function(toast) {
                            toast('Error submitting rating: ' + err.message);
                        });
                    }
                });
        },

        /**
         * Delete rating (toggle off)
         */
        deleteRating: function (itemId) {
            const self = this;

            if (!window.ApiClient) {
                return;
            }

            const baseUrl = ApiClient.serverAddress();
            const accessToken = ApiClient.accessToken();
            const deviceId = ApiClient.deviceId();
            const url = `${baseUrl}/Ratings/Items/${itemId}/Rating`;

            const authHeader = `MediaBrowser Client="Jellyfin Web", Device="Browser", DeviceId="${deviceId}", Version="10.11.0", Token="${accessToken}"`;

            const requestOptions = {
                method: 'DELETE',
                credentials: 'include',
                headers: {
                    'Content-Type': 'application/json',
                    'X-Emby-Authorization': authHeader
                }
            };

            fetch(url, requestOptions)
                .then(function(response) {
                    if (!response.ok) {
                        return response.text().then(function(errorText) {
                            throw new Error('HTTP ' + response.status + ': ' + errorText);
                        });
                    }
                    return response;
                })
                .then(function() {
                    // Clear the current rating
                    self.currentUserRating = 0;
                    self.updateStarDisplay(0);
                    self.loadRatings(itemId);

                    if (window.require) {
                        require(['toast'], function(toast) {
                            toast('Rating removed');
                        });
                    }
                })
                .catch(function(err) {
                    if (window.require) {
                        require(['toast'], function(toast) {
                            toast('Error removing rating: ' + err.message);
                        });
                    }
                });
        },

        /**
         * Show detailed ratings popup
         */
        showDetailedRatings: function (itemId) {
            const popup = document.getElementById('ratingsPluginPopup');
            const popupList = document.getElementById('ratingsPluginPopupList');

            popup.classList.add('visible');
            popupList.innerHTML = '<li class="ratings-plugin-popup-empty">Loading...</li>';

            ApiClient.getJSON(ApiClient.getUrl(`Ratings/Items/${itemId}/DetailedRatings`))
                .then(ratings => {
                    if (ratings && ratings.length > 0) {
                        let html = '';
                        ratings.forEach(rating => {
                            html += `
                                <li class="ratings-plugin-popup-item">
                                    <span class="ratings-plugin-popup-username">${this.escapeHtml(rating.Username)}</span>
                                    <span class="ratings-plugin-popup-rating">${rating.Rating}/10</span>
                                </li>
                            `;
                        });
                        popupList.innerHTML = html;
                    } else {
                        popupList.innerHTML = '<li class="ratings-plugin-popup-empty">No ratings yet</li>';
                    }
                })
                .catch(err => {
                    popupList.innerHTML = '<li class="ratings-plugin-popup-empty">Error loading ratings</li>';
                });
        },

        /**
         * Escape HTML to prevent XSS
         */
        escapeHtml: function (text) {
            const div = document.createElement('div');
            div.textContent = text;
            return div.innerHTML;
        },

        /**
         * Auto-change a pending request to processing when admin views it
         */
        autoProcessRequest: function (requestId) {
            const self = this;
            const baseUrl = ApiClient.serverAddress();
            const accessToken = ApiClient.accessToken();
            const deviceId = ApiClient.deviceId();
            const url = `${baseUrl}/Ratings/Requests/${requestId}/Status?status=processing`;
            const authHeader = `MediaBrowser Client="Jellyfin Web", Device="Browser", DeviceId="${deviceId}", Version="10.11.0", Token="${accessToken}"`;

            fetch(url, {
                method: 'POST',
                credentials: 'include',
                headers: { 'X-Emby-Authorization': authHeader }
            })
            .then(response => {
                if (response.ok) {
                    // Update the badge count
                    self.updateRequestBadge();
                }
            })
            .catch(err => {
                console.error('Error auto-processing request:', err);
            });
        },

        /**
         * Observe home page cards and add rating overlays
         */
        observeHomePageCards: function () {
            const self = this;

            // Use IntersectionObserver to only load ratings for visible cards
            const intersectionObserver = new IntersectionObserver((entries) => {
                entries.forEach(entry => {
                    if (entry.isIntersecting) {
                        const card = entry.target;

                        // Find the image container within this card
                        const imageContainer = card.querySelector('.cardImageContainer, .cardContent, .card-imageContainer');
                        if (!imageContainer) {
                            return;
                        }

                        // Skip if already has rating overlay
                        if (imageContainer.classList.contains('has-rating')) {
                            return;
                        }

                        // Get item ID from the card
                        const itemId = self.getItemIdFromCard(card);
                        if (!itemId) {
                            return;
                        }

                        // Fetch rating for this item (with caching)
                        self.addCardRating(imageContainer, itemId);

                        // Stop observing this card once we've processed it
                        // Note: Leaving badges are handled by updateDeletionBadges() which runs periodically
                        intersectionObserver.unobserve(card);
                    }
                });
            }, {
                rootMargin: '50px' // Start loading slightly before card comes into view
            });

            // Create MutationObserver to watch for new cards being added to DOM
            const mutationObserver = new MutationObserver(() => {
                // Find all cards that aren't being observed yet
                const cards = document.querySelectorAll('.card:not(.card .card)');
                cards.forEach(card => {
                    // Only observe if not already being watched
                    if (!card.dataset.ratingsObserved) {
                        card.dataset.ratingsObserved = 'true';
                        intersectionObserver.observe(card);
                    }
                });
            });

            // Start observing DOM for new cards
            mutationObserver.observe(document.body, {
                childList: true,
                subtree: true
            });

            // Initial scan for existing cards
            setTimeout(() => {
                const cards = document.querySelectorAll('.card:not(.card .card)');
                cards.forEach(card => {
                    card.dataset.ratingsObserved = 'true';
                    intersectionObserver.observe(card);
                });
            }, 2000);
        },

        /**
         * Get item ID from a card element
         */
        getItemIdFromCard: function (card) {
            // Try data-id attribute first (most reliable)
            const dataId = card.getAttribute('data-id');
            if (dataId && dataId.length === 32) {
                const dataType = card.getAttribute('data-type');
                const isFolder = card.getAttribute('data-isfolder');

                // Check data-type first (most reliable indicator)
                if (dataType === 'CollectionFolder' || dataType === 'UserView') {
                    return null; // Skip folders
                }

                // Allow Series, Movie, Episode, etc. even if data-isfolder="true"
                // (Series items have isfolder=true but are actual media items)
                if (dataType === 'Series' || dataType === 'Movie' || dataType === 'Episode' ||
                    dataType === 'Audio' || dataType === 'MusicAlbum' || dataType === 'Video') {
                    return dataId;
                }

                // If no recognized media type but has isfolder=true, skip it
                if (isFolder === 'true') {
                    return null;
                }

                return dataId;
            }

            // Try to find link with item ID
            const link = card.querySelector('a[href*="id="]');
            if (link) {
                // Skip library/folder navigation links (these have topParentId or parentId)
                if (link.href.includes('topParentId=') || link.href.includes('parentId=')) {
                    return null;
                }

                // Skip list views
                if (link.href.includes('#/list')) {
                    return null;
                }

                // Extract item ID - works for both #/details and #/tv?id= and #/movies?id= formats
                const match = link.href.match(/[?&]id=([a-f0-9]{32})/i);
                if (match) {
                    return match[1];
                }
            }

            // Try parent link
            const parentLink = card.closest('a[href*="id="]');
            if (parentLink) {
                // Skip library/folder navigation links
                if (parentLink.href.includes('topParentId=') || parentLink.href.includes('parentId=')) {
                    return null;
                }

                // Skip list views
                if (parentLink.href.includes('#/list')) {
                    return null;
                }

                // Extract item ID
                const match = parentLink.href.match(/[?&]id=([a-f0-9]{32})/i);
                if (match) {
                    return match[1];
                }
            }

            return null;
        },

        /**
         * Add rating overlay to a specific card
         */
        addCardRating: function (card, itemId) {
            if (!this.ratingsEnabled) return;
            const self = this;

            // Check cache first
            if (self.ratingsCache[itemId] !== undefined) {
                // Use cached data
                if (self.ratingsCache[itemId] !== null) {
                    const stats = self.ratingsCache[itemId];
                    self.createAndPositionOverlay(card, stats, itemId);
                }
                return;
            }

            const baseUrl = ApiClient.serverAddress();
            const accessToken = ApiClient.accessToken();
            const url = `${baseUrl}/Ratings/Items/${itemId}/Stats`;

            let deviceId = localStorage.getItem('_deviceId2');
            if (!deviceId) {
                deviceId = 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function(c) {
                    const r = Math.random() * 16 | 0;
                    const v = c === 'x' ? r : (r & 0x3 | 0x8);
                    return v.toString(16);
                });
                localStorage.setItem('_deviceId2', deviceId);
            }

            const authHeader = `MediaBrowser Client="Jellyfin Web", Device="Browser", DeviceId="${deviceId}", Version="10.11.0", Token="${accessToken}"`;

            fetch(url, {
                method: 'GET',
                credentials: 'include',
                headers: {
                    'Content-Type': 'application/json',
                    'X-Emby-Authorization': authHeader
                }
            })
                .then(response => {
                    if (!response.ok) {
                        throw new Error(`HTTP error! status: ${response.status}`);
                    }
                    return response.json();
                })
                .then(stats => {
                    // Only show if there's at least one rating
                    if (stats.TotalRatings > 0) {
                        // Cache the stats
                        self.ratingsCache[itemId] = stats;
                        self.createAndPositionOverlay(card, stats, itemId);
                    } else {
                        // Cache as null (no ratings)
                        self.ratingsCache[itemId] = null;
                    }
                })
                .catch(err => {
                    // Cache as null on error to avoid retrying
                    self.ratingsCache[itemId] = null;
                });
        },

        /**
         * Create and position overlay using CSS ::after pseudo-element
         * Combines rating AND leaving info in the same badge
         */
        createAndPositionOverlay: function (imageContainer, stats, itemId) {
            const self = this;

            // Build badge text - start with rating
            let badgeText = '★ ' + stats.AverageRating.toFixed(1);

            // Append leaving info if available
            if (itemId && self.scheduledDeletionsCache) {
                const deletion = self.scheduledDeletionsCache[itemId.toLowerCase()];
                if (deletion) {
                    const leavingText = self.formatLeavingText(deletion.DeleteAt);
                    badgeText += ' | ' + leavingText;
                }
            }

            // Use CSS ::after pseudo-element
            imageContainer.classList.add('has-rating');
            imageContainer.setAttribute('data-rating', badgeText);
        },

        /**
         * Format leaving text from deletion date
         */
        formatLeavingText: function (deleteAt) {
            const self = this;
            const now = new Date();
            const deleteDate = new Date(deleteAt);
            const diffMs = deleteDate - now;
            const diffDays = Math.ceil(diffMs / (1000 * 60 * 60 * 24));
            if (diffDays <= 0) return self.t('mediaLeavingIn') + ' Today';
            return self.t('mediaLeavingIn') + ' ' + diffDays + ' ' + self.t('mediaDays');
        },

        /**
         * Add leaving badge to a card - uses same CSS approach as rating badges
         * Called from IntersectionObserver to use exact same card detection
         */
        addLeavingBadgeToCard: function (imageContainer, itemId) {
            const self = this;

            // Skip if no cache or no itemId
            if (!self.scheduledDeletionsCache || !itemId) {
                return;
            }

            // Check if this item has scheduled deletion
            const deletion = self.scheduledDeletionsCache[itemId.toLowerCase()];
            if (!deletion) {
                return;
            }

            // Get the card element
            const card = imageContainer.closest('.card');
            if (!card) return;

            // Skip if already has leaving badge
            if (card.querySelector('.card-leaving-badge')) {
                return;
            }

            // Calculate days until deletion
            const now = new Date();
            const deleteDate = new Date(deletion.DeleteAt);
            const diffMs = deleteDate - now;
            const diffDays = Math.ceil(diffMs / (1000 * 60 * 60 * 24));
            const text = diffDays <= 0 ? self.t('mediaLeavingIn') + ' Today' : self.t('mediaLeavingIn') + ' ' + diffDays + ' ' + self.t('mediaDays');

            // Create badge and add to card (not imageContainer - that has overflow:hidden)
            const badge = document.createElement('div');
            badge.className = 'card-leaving-badge';
            badge.textContent = text;
            card.appendChild(badge);

        },

        /**
         * Initialize Request Media Button - Completely isolated and safe
         */
        initRequestButton: function () {
            const self = this;
            try {
                // Check if already exists
                if (document.getElementById('requestMediaBtn')) {
                    return;
                }

                // Create button with position relative for badge
                const btn = document.createElement('button');
                btn.id = 'requestMediaBtn';
                btn.style.position = 'relative';
                btn.innerHTML = '<span class="btn-text">' + self.t('requestMedia') + '</span>';
                btn.setAttribute('type', 'button');
                btn.setAttribute('data-tooltip', 'Request movies or TV series from admin');

                // Update badge periodically
                self.updateRequestBadge(btn);
                setInterval(() => self.updateRequestBadge(btn), 30000); // Update every 30 seconds

                // Create modal
                const modal = document.createElement('div');
                modal.id = 'requestMediaModal';
                modal.innerHTML = `
                    <div id="requestMediaModalContent">
                        <button id="requestMediaModalClose" type="button">&times;</button>
                        <div id="requestMediaModalTitle">Request Media</div>
                        <div id="requestMediaModalBody">
                            <p style="text-align: center; color: #999;">Loading...</p>
                        </div>
                    </div>
                `;

                // Add to DOM - append to header container so they scroll with header
                const headerContainer = document.querySelector('.headerTabs, .skinHeader');
                if (headerContainer) {
                    // Make header container position relative so absolute positioning works
                    headerContainer.style.position = 'relative';
                    headerContainer.appendChild(btn);
                } else {
                    document.body.appendChild(btn);
                }
                document.body.appendChild(modal);

                // Button click - wrapped in try-catch
                btn.addEventListener('click', (e) => {
                    try {
                        e.preventDefault();
                        e.stopPropagation();
                        modal.classList.add('show');
                        document.body.style.overflow = 'hidden';
                        self.loadRequestInterface();
                    } catch (err) {
                        console.error('Button click error:', err);
                    }
                });

                // Close button - wrapped in try-catch
                const closeBtn = document.getElementById('requestMediaModalClose');
                if (closeBtn) {
                    closeBtn.addEventListener('click', (e) => {
                        try {
                            e.preventDefault();
                            e.stopPropagation();
                            modal.classList.remove('show');
                            document.body.style.overflow = '';
                        } catch (err) {
                            console.error('Close button error:', err);
                        }
                    });
                }

                // Click outside to close - wrapped in try-catch
                modal.addEventListener('click', (e) => {
                    try {
                        if (e.target === modal) {
                            e.preventDefault();
                            e.stopPropagation();
                            modal.classList.remove('show');
                            document.body.style.overflow = '';
                        }
                    } catch (err) {
                        console.error('Modal click error:', err);
                    }
                });

                // Hide during video playback and on login page - wrapped in try-catch
                setInterval(() => {
                    try {
                        const videoPlayer = document.querySelector('.videoPlayerContainer');
                        const isVideoPlaying = videoPlayer && !videoPlayer.classList.contains('hide');

                        // Check if on login page
                        const isLoginPage = self.isOnLoginPage();

                        if (isVideoPlaying || isLoginPage) {
                            btn.classList.add('hidden');
                        } else {
                            btn.classList.remove('hidden');
                        }
                    } catch (err) {
                        // Silently fail - don't break anything
                    }
                }, 1000);

                // Listen for user changes to clear cache
                self.setupUserChangeListener();

            } catch (err) {
                console.error('Request button initialization failed:', err);
                // Fail silently - don't break the plugin
            }
        },

        /**
         * Check if currently on login page
         */
        isOnLoginPage: function () {
            try {
                // Check URL for login indicators
                const url = window.location.href.toLowerCase();
                const hash = window.location.hash.toLowerCase();

                // Check URL patterns for login/startup pages
                if (url.includes('/login') ||
                    url.includes('/startup') ||
                    hash.includes('login') ||
                    hash.includes('startup') ||
                    hash === '#' ||
                    hash === '') {

                    // Double check - if there's a login form visible, it's definitely login page
                    const loginForm = document.querySelector('.loginPage, #loginPage, .manualLoginForm, #manualLoginForm, .selectServer');
                    if (loginForm) {
                        return true;
                    }

                    // If URL suggests login but no login form, check if user exists
                    if (window.ApiClient && ApiClient.getCurrentUserId()) {
                        return false; // User is logged in, not a login page
                    }

                    // Only return true for login URL if we're sure
                    if (hash.includes('login') || url.includes('/login')) {
                        return true;
                    }
                }

                // Check if login form is visible (definitive check)
                const loginForm = document.querySelector('.loginPage, #loginPage, .manualLoginForm, #manualLoginForm');
                if (loginForm && loginForm.offsetParent !== null) {
                    return true;
                }

                return false;
            } catch (err) {
                return false;
            }
        },

        /**
         * Setup listener for user changes (login/logout)
         * Manages notification session lifecycle
         */
        setupUserChangeListener: function () {
            const self = this;
            try {
                // Store current user ID to detect changes
                let lastUserId = window.ApiClient ? ApiClient.getCurrentUserId() : null;

                // Check for user changes periodically
                setInterval(() => {
                    try {
                        const currentUserId = window.ApiClient ? ApiClient.getCurrentUserId() : null;

                        // User changed (login/logout/switch account)
                        if (currentUserId !== lastUserId) {

                            // Clear all cached data
                            self.clearRequestCache();

                            // Handle notification session lifecycle
                            if (lastUserId && !currentUserId) {
                                // User logged out - clear notification session and stop polling
                                self.clearNotificationSession();
                                self.stopNotificationPolling();
                            } else if (!lastUserId && currentUserId) {
                                // User just logged in - start new notification session
                                self.startNotificationSession();
                                // Reinitialize notifications for new user
                                if (self.notificationsEnabled) {
                                    self.createNotificationContainer();
                                    self.startNotificationPolling();
                                } else {
                                    // Check if notifications are enabled and start if so
                                    self.initNotifications();
                                }
                            } else if (lastUserId && currentUserId && lastUserId !== currentUserId) {
                                // User switched accounts - clear old session and start new one
                                self.clearNotificationSession();
                                self.stopNotificationPolling();
                                self.startNotificationSession();
                                // Reinitialize notifications for new user
                                if (self.notificationsEnabled) {
                                    self.createNotificationContainer();
                                    self.startNotificationPolling();
                                } else {
                                    self.initNotifications();
                                }
                            }

                            lastUserId = currentUserId;

                            // Update badge for new user
                            const btn = document.getElementById('requestMediaBtn');
                            if (btn && currentUserId) {
                                self.updateRequestBadge(btn);
                            }

                            // Test notification button disabled - use TV app for testing
                            // const testBtn = document.getElementById('testNotificationBtn');
                            // if (testBtn) {
                            //     testBtn.remove();
                            // }
                            // if (currentUserId) {
                            //     self.initTestNotificationButton();
                            // }
                        }
                    } catch (err) {
                        // Silently fail
                    }
                }, 2000);

                // Also listen for Jellyfin events if available
                if (window.Events) {
                    Events.on(ApiClient, 'authenticated', () => {
                        self.clearRequestCache();
                        const btn = document.getElementById('requestMediaBtn');
                        if (btn) {
                            self.updateRequestBadge(btn);
                        }
                        // Start notification session on authentication
                        if (!self.notificationSessionUserId) {
                            self.startNotificationSession();
                            self.initNotifications();
                        }
                    });
                }
            } catch (err) {
                console.error('Error setting up user change listener:', err);
            }
        },

        /**
         * Clear all cached request data
         */
        clearRequestCache: function () {
            try {
                // Clear viewed request IDs
                localStorage.removeItem('ratings_viewed_requests');

                // Clear any other cached data related to requests
                const keysToRemove = [];
                for (let i = 0; i < localStorage.length; i++) {
                    const key = localStorage.key(i);
                    if (key && key.startsWith('ratings_')) {
                        keysToRemove.push(key);
                    }
                }
                keysToRemove.forEach(key => localStorage.removeItem(key));

            } catch (err) {
                console.error('Error clearing request cache:', err);
            }
        },

        /**
         * Initialize search field in header
         */
        initSearchField: function () {
            const self = this;
            try {
                // Check if already exists
                if (document.getElementById('headerSearchField')) {
                    return;
                }

                // Check config if search button should be shown
                const checkConfigAndCreate = () => {
                    if (!window.ApiClient) {
                        setTimeout(checkConfigAndCreate, 1000);
                        return;
                    }
                    const baseUrl = ApiClient.serverAddress();
                    fetch(`${baseUrl}/Ratings/Config`, { method: 'GET', credentials: 'include' })
                        .then(response => response.json())
                        .then(config => {
                            if (config.ShowSearchButton === false) {
                                return; // Don't create search field
                            }
                            createSearchField();
                        })
                        .catch(() => {
                            // Default to showing if config fails
                            createSearchField();
                        });
                };

                // Wait for DOM to be ready
                const createSearchField = () => {
                    try {
                        // Check if already exists
                        if (document.getElementById('headerSearchField')) {
                            return;
                        }

                        // Create search container
                        const searchContainer = document.createElement('div');
                        searchContainer.id = 'headerSearchField';

                        // Create search input
                        const searchInput = document.createElement('input');
                        searchInput.type = 'text';
                        searchInput.placeholder = 'Search...';
                        searchInput.id = 'headerSearchInput';
                        searchInput.autocomplete = 'off';
                        searchInput.setAttribute('autocomplete', 'off');
                        searchInput.setAttribute('autocorrect', 'off');
                        searchInput.setAttribute('autocapitalize', 'off');
                        searchInput.setAttribute('spellcheck', 'false');

                        // Create search icon
                        const searchIcon = document.createElement('span');
                        searchIcon.id = 'headerSearchIcon';
                        searchIcon.innerHTML = '🔍';

                        // Create dropdown container - append to body to avoid stacking context issues
                        const searchDropdown = document.createElement('div');
                        searchDropdown.id = 'searchDropdown';
                        document.body.appendChild(searchDropdown);

                        // Append elements to search container
                        searchContainer.appendChild(searchIcon);
                        searchContainer.appendChild(searchInput);

                        // Append to header container so it scrolls with header
                        const headerContainer = document.querySelector('.headerTabs, .skinHeader');
                        if (headerContainer) {
                            headerContainer.style.position = 'relative';
                            headerContainer.appendChild(searchContainer);
                        } else {
                            document.body.appendChild(searchContainer);
                        }

                        // Trigger responsive scaling after element is added (fixes mobile positioning)
                        setTimeout(() => {
                            if (typeof self.triggerResponsiveUpdate === 'function') {
                                self.triggerResponsiveUpdate();
                            }
                        }, 100);

                        // Real-time search with dropdown
                        let searchTimeout;
                        searchInput.addEventListener('input', function() {
                            // Update icon based on input
                            if (searchInput.value.trim()) {
                                searchIcon.innerHTML = '✕';
                                searchIcon.style.fontSize = '20px';
                            } else {
                                searchIcon.innerHTML = '🔍';
                                searchIcon.style.fontSize = '18px';
                            }

                            clearTimeout(searchTimeout);
                            const query = searchInput.value.trim();

                            if (!query) {
                                // Hide dropdown when empty
                                self.hideSearchDropdown();
                                return;
                            }

                            // Show loading state and position dropdown
                            searchDropdown.innerHTML = '<div class="dropdown-loading">Searching...</div>';
                            searchDropdown.classList.add('visible');
                            self.positionSearchDropdown();

                            searchTimeout = setTimeout(() => {
                                // Search entire library and show in dropdown
                                self.searchLibraryDropdown(query);
                            }, 300); // Debounce for performance
                        });

                        // Handle enter key - select first result
                        searchInput.addEventListener('keypress', function(e) {
                            if (e.key === 'Enter') {
                                const firstItem = searchDropdown.querySelector('.dropdown-item');
                                if (firstItem) {
                                    firstItem.click();
                                }
                            }
                        });

                        // Handle escape key - close dropdown
                        searchInput.addEventListener('keydown', function(e) {
                            if (e.key === 'Escape') {
                                self.hideSearchDropdown();
                                searchInput.blur();
                            }
                        });

                        // Handle icon click - clear search or focus
                        searchIcon.addEventListener('click', function() {
                            if (searchInput.value.trim()) {
                                searchInput.value = '';
                                searchIcon.innerHTML = '🔍';
                                searchIcon.style.fontSize = '18px';
                                self.hideSearchDropdown();
                            } else {
                                searchInput.focus();
                            }
                        });

                        // Close dropdown when clicking outside
                        document.addEventListener('click', function(e) {
                            const dropdown = document.getElementById('searchDropdown');
                            if (!searchContainer.contains(e.target) && (!dropdown || !dropdown.contains(e.target))) {
                                self.hideSearchDropdown();
                            }
                        });

                        // Hide during video playback and on login page
                        setInterval(() => {
                            try {
                                const videoPlayer = document.querySelector('.videoPlayerContainer');
                                const isVideoPlaying = videoPlayer && !videoPlayer.classList.contains('hide');
                                const isLoginPage = self.isOnLoginPage();

                                if (isVideoPlaying || isLoginPage) {
                                    searchContainer.classList.add('hidden');
                                    self.hideSearchDropdown();
                                } else {
                                    searchContainer.classList.remove('hidden');
                                }
                            } catch (err) {
                                // Silently fail
                            }
                        }, 1000);

                    } catch (err) {
                        console.error('Error creating search field:', err);
                    }
                };

                // Try to create immediately (check config first)
                setTimeout(checkConfigAndCreate, 1500);

                // Also try on page visibility change
                document.addEventListener('visibilitychange', () => {
                    if (document.visibilityState === 'visible' && !document.getElementById('headerSearchField')) {
                        setTimeout(checkConfigAndCreate, 500);
                    }
                });

                // Listen for Jellyfin navigation events
                try {
                    if (window.Emby && window.Emby.Page && typeof Emby.Page.addEventListener === 'function') {
                        Emby.Page.addEventListener('pageshow', () => {
                            if (!document.getElementById('headerSearchField')) {
                                setTimeout(checkConfigAndCreate, 500);
                            } else {
                                // Clear search when navigating to a new page
                                const searchInput = document.getElementById('headerSearchInput');
                                const searchIcon = document.getElementById('headerSearchIcon');
                                if (searchInput && searchInput.value.trim()) {
                                    searchInput.value = '';
                                    if (searchIcon) {
                                        searchIcon.innerHTML = '🔍';
                                        searchIcon.style.fontSize = '18px';
                                    }
                                    self.filterCurrentPageContent('');
                                }
                            }
                        });
                    }
                } catch (e) {
                    // Emby.Page.addEventListener not available
                }

                // Monitor for URL changes to clear search (works for SPA navigation)
                let lastUrl = window.location.href;
                setInterval(() => {
                    try {
                        const currentUrl = window.location.href;
                        if (currentUrl !== lastUrl) {
                            lastUrl = currentUrl;
                            const searchInput = document.getElementById('headerSearchInput');
                            const searchIcon = document.getElementById('headerSearchIcon');
                            if (searchInput && searchInput.value.trim()) {
                                searchInput.value = '';
                                if (searchIcon) {
                                    searchIcon.innerHTML = '🔍';
                                    searchIcon.style.fontSize = '18px';
                                }
                                // Reset filters and clear full library search
                                self.filterCurrentPageContent('');
                                self.clearFullLibrarySearch();
                            } else {
                                // Also clear full library search when URL changes even if search is empty
                                self.clearFullLibrarySearch();
                            }
                        }
                    } catch (err) {
                        // Silently fail
                    }
                }, 500);

                // Also listen for hash changes (SPA navigation)
                window.addEventListener('hashchange', () => {
                    try {
                        const searchInput = document.getElementById('headerSearchInput');
                        const searchIcon = document.getElementById('headerSearchIcon');
                        if (searchInput && searchInput.value.trim()) {
                            searchInput.value = '';
                            if (searchIcon) {
                                searchIcon.innerHTML = '🔍';
                                searchIcon.style.fontSize = '18px';
                            }
                            self.filterCurrentPageContent('');
                        }
                    } catch (err) {
                        // Silently fail
                    }
                });

            } catch (err) {
                console.error('Search field initialization failed:', err);
            }
        },

        /**
         * Initialize notification toggle in header
         */
        initNotificationToggle: function () {
            const self = this;
            try {
                // Check if already exists
                if (document.getElementById('notificationToggle')) {
                    return;
                }

                // Check config if notifications are enabled
                const checkConfigAndCreate = () => {
                    if (!window.ApiClient) {
                        setTimeout(checkConfigAndCreate, 1000);
                        return;
                    }
                    const baseUrl = ApiClient.serverAddress();
                    fetch(`${baseUrl}/Ratings/Config`, { method: 'GET', credentials: 'include' })
                        .then(response => response.json())
                        .then(config => {
                            if (config.EnableNewMediaNotifications === false) {
                                return; // Don't create notification toggle
                            }
                            createNotificationToggle();
                        })
                        .catch(() => {
                            // Default to showing if config fails
                            createNotificationToggle();
                        });
                };

                const createNotificationToggle = () => {
                    try {
                        // Check if already exists
                        if (document.getElementById('notificationToggle')) {
                            return;
                        }

                        // Create toggle container
                        const toggleContainer = document.createElement('div');
                        toggleContainer.id = 'notificationToggle';

                        // Create bell icon (always shows bell)
                        const bellIcon = document.createElement('span');
                        bellIcon.id = 'notificationToggleIcon';
                        bellIcon.innerHTML = '🔔';

                        // Create tooltip
                        const tooltip = document.createElement('div');
                        tooltip.id = 'notificationTooltip';
                        tooltip.textContent = 'Enable/disable new media notifications';

                        // Get saved preference (default to enabled)
                        const savedPref = localStorage.getItem('ratingsNotificationsEnabled');
                        const isEnabled = savedPref === null ? true : savedPref === 'true';

                        // Update visual state - toggle red cross lines via disabled class
                        const updateToggleState = (enabled) => {
                            if (enabled) {
                                toggleContainer.classList.remove('disabled');
                            } else {
                                toggleContainer.classList.add('disabled');
                            }
                            // Store in localStorage for this user
                            localStorage.setItem('ratingsNotificationsEnabled', enabled.toString());
                            // Also store in self for notification checking
                            self.userNotificationsEnabled = enabled;
                        };

                        // Set initial state
                        updateToggleState(isEnabled);
                        self.userNotificationsEnabled = isEnabled;

                        // Auto-hide tooltip after delay
                        let tooltipTimer = null;
                        const showTooltipFor = (text, duration) => {
                            if (tooltipTimer) clearTimeout(tooltipTimer);
                            tooltip.textContent = text;
                            const rect = toggleContainer.getBoundingClientRect();
                            tooltip.style.top = (rect.bottom + 8) + 'px';
                            tooltip.style.left = (rect.left + rect.width / 2) + 'px';
                            tooltip.style.transform = 'translateX(-50%)';
                            tooltip.classList.add('show');
                            tooltipTimer = setTimeout(() => {
                                tooltip.classList.remove('show');
                                tooltipTimer = null;
                            }, duration);
                        };

                        // Handle click
                        toggleContainer.addEventListener('click', () => {
                            const currentState = self.userNotificationsEnabled;
                            const newState = !currentState;
                            updateToggleState(newState);
                            showTooltipFor(newState ? 'Notifications enabled' : 'Notifications disabled', 2000);
                        });

                        // Append elements
                        toggleContainer.appendChild(bellIcon);
                        document.body.appendChild(tooltip); // Tooltip in body to avoid clipping

                        // Show tooltip on hover, auto-hide after 2s
                        toggleContainer.addEventListener('mouseenter', () => {
                            showTooltipFor('Enable/disable new media notifications', 2000);
                        });

                        toggleContainer.addEventListener('mouseleave', () => {
                            if (tooltipTimer) clearTimeout(tooltipTimer);
                            tooltip.classList.remove('show');
                            tooltipTimer = null;
                        });

                        // Append to header container
                        const headerContainer = document.querySelector('.headerTabs, .skinHeader');
                        if (headerContainer) {
                            headerContainer.style.position = 'relative';
                            headerContainer.appendChild(toggleContainer);
                        } else {
                            document.body.appendChild(toggleContainer);
                        }

                        // Hide during video playback and on login page
                        setInterval(() => {
                            try {
                                const videoPlayer = document.querySelector('.videoPlayerContainer');
                                const isVideoPlaying = videoPlayer && !videoPlayer.classList.contains('hide');
                                const isLoginPage = self.isOnLoginPage();

                                if (isVideoPlaying || isLoginPage) {
                                    toggleContainer.classList.add('hidden');
                                } else {
                                    toggleContainer.classList.remove('hidden');
                                }
                            } catch (err) {
                                // Silently fail
                            }
                        }, 1000);

                    } catch (err) {
                        console.error('Error creating notification toggle:', err);
                    }
                };

                // Try to create after a delay (check config first)
                setTimeout(checkConfigAndCreate, 1600);

                // Also try on page visibility change
                document.addEventListener('visibilitychange', () => {
                    if (document.visibilityState === 'visible' && !document.getElementById('notificationToggle')) {
                        setTimeout(checkConfigAndCreate, 500);
                    }
                });

            } catch (err) {
                console.error('Notification toggle initialization failed:', err);
            }
        },

        /**
         * Initialize latest media button (replaces Sync Play button)
         */
        initLatestMediaButton: function () {
            const self = this;
            try {
                // Check if already exists
                if (document.getElementById('latestMediaBtn')) {
                    return;
                }

                // Check config if latest media button is enabled
                const checkConfigAndCreate = () => {
                    if (!window.ApiClient) {
                        setTimeout(checkConfigAndCreate, 1000);
                        return;
                    }
                    const baseUrl = ApiClient.serverAddress();
                    fetch(`${baseUrl}/Ratings/Config`, { method: 'GET', credentials: 'include' })
                        .then(response => response.json())
                        .then(config => {
                            if (config.ShowLatestMediaButton === false) {
                                return; // Don't create button
                            }
                            createLatestMediaButton();
                        })
                        .catch(() => {
                            // Default to showing if config fails
                            createLatestMediaButton();
                        });
                };

                const createLatestMediaButton = () => {
                    try {
                        // Check if already exists
                        if (document.getElementById('latestMediaBtn')) {
                            return;
                        }

                        // Find and hide the Sync Play button
                        const syncPlayBtn = document.querySelector('.headerSyncButton');
                        if (syncPlayBtn) {
                            syncPlayBtn.style.display = 'none';
                        }

                        // Create the latest media button
                        const btn = document.createElement('button');
                        btn.id = 'latestMediaBtn';
                        btn.className = 'headerButton headerButtonRight paper-icon-button-light';
                        btn.setAttribute('type', 'button');
                        btn.setAttribute('title', self.t('latestMedia'));
                        btn.style.position = 'relative';
                        // Clock/new icon - represents "latest/recent"
                        btn.innerHTML = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="currentColor">
                            <path d="M13 3a9 9 0 0 0-9 9H1l3.89 3.89.07.14L9 12H6c0-3.87 3.13-7 7-7s7 3.13 7 7-3.13 7-7 7c-1.93 0-3.68-.79-4.94-2.06l-1.42 1.42A8.954 8.954 0 0 0 13 21a9 9 0 0 0 0-18zm-1 5v5l4.28 2.54.72-1.21-3.5-2.08V8H12z"/>
                        </svg><span id="latestMediaBadge" class="latest-media-badge"></span>`;

                        // Update badge count periodically
                        self.updateLatestMediaBadge();
                        setInterval(() => self.updateLatestMediaBadge(), 60000); // Update every minute

                        // Create dropdown container
                        const dropdown = document.createElement('div');
                        dropdown.id = 'latestMediaDropdown';
                        document.body.appendChild(dropdown);

                        // Position dropdown below button
                        const positionDropdown = () => {
                            const rect = btn.getBoundingClientRect();
                            dropdown.style.top = (rect.bottom + 4) + 'px';
                            dropdown.style.right = (window.innerWidth - rect.right) + 'px';
                            dropdown.style.left = 'auto';
                        };

                        // Toggle dropdown on click
                        btn.addEventListener('click', (e) => {
                            e.preventDefault();
                            e.stopPropagation();

                            if (dropdown.classList.contains('visible')) {
                                dropdown.classList.remove('visible');
                            } else {
                                positionDropdown();
                                dropdown.classList.add('visible');
                                self.loadLatestMedia(dropdown);
                                // Mark as seen - clear badge
                                localStorage.setItem('ratings_latest_media_seen', new Date().toISOString());
                                const badge = document.getElementById('latestMediaBadge');
                                if (badge) {
                                    badge.classList.remove('visible');
                                    badge.textContent = '';
                                }
                            }
                        });

                        // Close dropdown when clicking outside
                        document.addEventListener('click', (e) => {
                            if (!btn.contains(e.target) && !dropdown.contains(e.target)) {
                                dropdown.classList.remove('visible');
                            }
                        });

                        // Close dropdown on escape
                        document.addEventListener('keydown', (e) => {
                            if (e.key === 'Escape') {
                                dropdown.classList.remove('visible');
                            }
                        });

                        // Insert button in header - try to find headerRight or similar container
                        const headerRight = document.querySelector('.headerRight');
                        if (headerRight) {
                            // Insert at the beginning of headerRight
                            headerRight.insertBefore(btn, headerRight.firstChild);
                        } else {
                            // Fallback: find skinHeader and append
                            const skinHeader = document.querySelector('.skinHeader');
                            if (skinHeader) {
                                skinHeader.appendChild(btn);
                            } else {
                                document.body.appendChild(btn);
                            }
                        }

                        // Trigger responsive scaling after element is added (fixes mobile positioning)
                        setTimeout(() => {
                            if (typeof self.triggerResponsiveUpdate === 'function') {
                                self.triggerResponsiveUpdate();
                            }
                        }, 100);

                        // Observe for Sync Play button appearing later (SPA navigation)
                        const observer = new MutationObserver(() => {
                            const syncBtn = document.querySelector('.headerSyncButton');
                            if (syncBtn && syncBtn.style.display !== 'none') {
                                syncBtn.style.display = 'none';
                            }
                        });
                        observer.observe(document.body, { childList: true, subtree: true });

                        // Hide during video playback and on login page
                        setInterval(() => {
                            try {
                                const videoPlayer = document.querySelector('.videoPlayerContainer');
                                const isVideoPlaying = videoPlayer && !videoPlayer.classList.contains('hide');
                                const isLoginPage = self.isOnLoginPage();

                                if (isVideoPlaying || isLoginPage) {
                                    btn.classList.add('hidden');
                                    dropdown.classList.remove('visible');
                                } else {
                                    btn.classList.remove('hidden');
                                }
                            } catch (err) {
                                // Silently fail
                            }
                        }, 1000);

                    } catch (err) {
                        console.error('Error creating latest media button:', err);
                    }
                };

                // Try to create after a delay
                setTimeout(checkConfigAndCreate, 1700);

                // Also try on page visibility change
                document.addEventListener('visibilitychange', () => {
                    if (document.visibilityState === 'visible' && !document.getElementById('latestMediaBtn')) {
                        setTimeout(checkConfigAndCreate, 500);
                    }
                });

            } catch (err) {
                console.error('Latest media button initialization failed:', err);
            }
        },

        /**
         * Update the badge count on Latest Media button
         */
        updateLatestMediaBadge: function () {
            const self = this;
            if (!window.ApiClient) return;

            const userId = ApiClient.getCurrentUserId();
            const baseUrl = ApiClient.serverAddress();
            const authHeader = ApiClient._serverInfo?.AccessToken ?
                `MediaBrowser Client="Jellyfin Web", Device="Browser", DeviceId="${ApiClient._deviceId}", Version="${ApiClient._appVersion}", Token="${ApiClient._serverInfo.AccessToken}"` : '';

            // Get last seen time
            const lastSeenStr = localStorage.getItem('ratings_latest_media_seen');
            const lastSeen = lastSeenStr ? new Date(lastSeenStr) : new Date(0);

            // Fetch latest items
            fetch(`${baseUrl}/Users/${userId}/Items?SortBy=DateCreated&SortOrder=Descending&IncludeItemTypes=Movie,Series,Episode&Recursive=true&Limit=50&Fields=DateCreated,SeriesId`, {
                method: 'GET',
                credentials: 'include',
                headers: { 'X-Emby-Authorization': authHeader }
            })
            .then(r => r.json())
            .then(data => {
                const items = data.Items || [];
                // Count items newer than last seen, deduplicate series by SeriesId
                const seenSeries = new Set();
                let newCount = 0;

                items.forEach(item => {
                    const itemDate = new Date(item.DateCreated);
                    if (itemDate > lastSeen) {
                        if (item.Type === 'Episode') {
                            // For episodes, count the series once
                            if (item.SeriesId && !seenSeries.has(item.SeriesId)) {
                                seenSeries.add(item.SeriesId);
                                newCount++;
                            }
                        } else {
                            // Movies and Series
                            if (!seenSeries.has(item.Id)) {
                                seenSeries.add(item.Id);
                                newCount++;
                            }
                        }
                    }
                });

                // Update badge
                const badge = document.getElementById('latestMediaBadge');
                if (badge) {
                    if (newCount > 0) {
                        badge.textContent = newCount > 99 ? '99+' : newCount;
                        badge.classList.add('visible');
                    } else {
                        badge.classList.remove('visible');
                        badge.textContent = '';
                    }
                }
            })
            .catch(err => {
                console.error('Failed to update latest media badge:', err);
            });
        },

        /**
         * Load latest media items into dropdown
         */
        loadLatestMedia: function (dropdown) {
            const self = this;

            // Show loading state
            dropdown.innerHTML = `<div class="latest-header">${self.t('latestMedia')}</div><div class="latest-loading">${self.t('latestMediaLoading')}</div>`;

            if (!window.ApiClient) {
                dropdown.innerHTML = `<div class="latest-header">${self.t('latestMedia')}</div><div class="latest-empty">${self.t('latestMediaError')}</div>`;
                return;
            }

            const userId = ApiClient.getCurrentUserId();
            const baseUrl = ApiClient.serverAddress();
            const authHeader = ApiClient._serverInfo?.AccessToken ?
                `MediaBrowser Client="Jellyfin Web", Device="Browser", DeviceId="${ApiClient._deviceId}", Version="${ApiClient._appVersion}", Token="${ApiClient._serverInfo.AccessToken}"` : '';

            // Fetch both: 1) new movies/series 2) latest episodes (to detect series with new content)
            Promise.all([
                // New movies and series
                fetch(`${baseUrl}/Users/${userId}/Items?SortBy=DateCreated&SortOrder=Descending&IncludeItemTypes=Movie,Series&Recursive=true&Limit=30&Fields=PrimaryImageAspectRatio,Genres,ProductionYear,DateCreated`, {
                    method: 'GET',
                    credentials: 'include',
                    headers: { 'X-Emby-Authorization': authHeader }
                }).then(r => r.json()),
                // Latest episodes (to find series with new episodes)
                fetch(`${baseUrl}/Users/${userId}/Items?SortBy=DateCreated&SortOrder=Descending&IncludeItemTypes=Episode&Recursive=true&Limit=100&Fields=SeriesId,SeriesName,DateCreated`, {
                    method: 'GET',
                    credentials: 'include',
                    headers: { 'X-Emby-Authorization': authHeader }
                }).then(r => r.json())
            ])
            .then(([mediaData, episodeData]) => {
                // Helper function to format time ago
                const formatTimeAgo = (dateString) => {
                    if (!dateString) return '';
                    const date = new Date(dateString);
                    const now = new Date();
                    const diffMs = now - date;
                    const diffMins = Math.floor(diffMs / 60000);
                    const diffHours = Math.floor(diffMs / 3600000);
                    const diffDays = Math.floor(diffMs / 86400000);

                    if (diffMins < 1) return self.t('timeJustNow');
                    if (diffMins < 60) return `${diffMins} ${self.t('timeMinutes')} ${self.t('timeAgo')}`;
                    if (diffHours < 24) return `${diffHours} ${self.t('timeHours')} ${self.t('timeAgo')}`;
                    return `${diffDays} ${self.t('timeDays')} ${self.t('timeAgo')}`;
                };

                // Helper function to clean title (remove IMDB IDs)
                const cleanTitle = (title) => {
                    if (!title) return 'Unknown';
                    return title
                        .replace(/\s*\[imdbid[-:]?tt\d+\]/gi, '')
                        .replace(/\s*\[tt\d+\]/gi, '')
                        .replace(/\s*\(tt\d+\)/gi, '')
                        .trim();
                };

                // Track series IDs from new media (these are completely new series)
                const newSeriesIds = new Set();
                const mediaItems = mediaData.Items || [];
                mediaItems.forEach(item => {
                    if (item.Type === 'Series') {
                        newSeriesIds.add(item.Id);
                    }
                });

                // Find series with new episodes (that aren't completely new series)
                const seriesWithNewEpisodes = new Map(); // seriesId -> { count, latestDate, seriesName }
                const episodes = episodeData.Items || [];
                episodes.forEach(ep => {
                    if (ep.SeriesId && !newSeriesIds.has(ep.SeriesId)) {
                        if (!seriesWithNewEpisodes.has(ep.SeriesId)) {
                            seriesWithNewEpisodes.set(ep.SeriesId, {
                                count: 1,
                                latestDate: ep.DateCreated,
                                seriesName: ep.SeriesName,
                                seriesId: ep.SeriesId
                            });
                        } else {
                            const existing = seriesWithNewEpisodes.get(ep.SeriesId);
                            existing.count++;
                            if (new Date(ep.DateCreated) > new Date(existing.latestDate)) {
                                existing.latestDate = ep.DateCreated;
                            }
                        }
                    }
                });

                // Fetch series details for those with new episodes
                const seriesPromises = Array.from(seriesWithNewEpisodes.keys()).slice(0, 20).map(seriesId =>
                    fetch(`${baseUrl}/Users/${userId}/Items/${seriesId}?Fields=PrimaryImageAspectRatio,Genres,ProductionYear`, {
                        method: 'GET',
                        credentials: 'include',
                        headers: { 'X-Emby-Authorization': authHeader }
                    }).then(r => r.ok ? r.json() : null).catch(() => null)
                );

                return Promise.all(seriesPromises).then(seriesDetails => {
                    // Build combined list
                    const combinedItems = [];

                    // Add new movies/series
                    mediaItems.forEach(item => {
                        combinedItems.push({
                            ...item,
                            isNewMedia: true,
                            newEpisodeCount: 0,
                            sortDate: new Date(item.DateCreated)
                        });
                    });

                    // Add series with new episodes
                    seriesDetails.forEach(series => {
                        if (series && seriesWithNewEpisodes.has(series.Id)) {
                            const epInfo = seriesWithNewEpisodes.get(series.Id);
                            combinedItems.push({
                                ...series,
                                isNewMedia: false,
                                newEpisodeCount: epInfo.count,
                                sortDate: new Date(epInfo.latestDate),
                                DateCreated: epInfo.latestDate
                            });
                        }
                    });

                    // Sort by date descending
                    combinedItems.sort((a, b) => b.sortDate - a.sortDate);

                    // Deduplicate by ID
                    const seen = new Set();
                    const uniqueItems = combinedItems.filter(item => {
                        if (seen.has(item.Id)) return false;
                        seen.add(item.Id);
                        return true;
                    }).slice(0, 30);

                    if (uniqueItems.length === 0) {
                        dropdown.innerHTML = `<div class="latest-header">${self.t('latestMedia')}</div><div class="latest-empty">${self.t('latestMediaEmpty')}</div>`;
                        return;
                    }

                    let html = `<div class="latest-header">${self.t('latestMedia')}</div>`;

                    uniqueItems.forEach(item => {
                        const itemId = item.Id;
                        const itemName = cleanTitle(item.Name);
                        const itemYear = item.ProductionYear || '';
                        const itemType = item.Type;
                        const genres = item.Genres || [];
                        const timeAgo = formatTimeAgo(item.DateCreated);

                        // Determine display type
                        let displayType = 'other';
                        let typeLabel = self.t('typeOther');

                        if (itemType === 'Movie') {
                            if (genres.some(g => g.toLowerCase() === 'anime' || g.toLowerCase() === 'animation')) {
                                displayType = 'anime';
                                typeLabel = self.t('typeAnime');
                            } else {
                                displayType = 'movie';
                                typeLabel = self.t('typeMovie');
                            }
                        } else if (itemType === 'Series') {
                            if (genres.some(g => g.toLowerCase() === 'anime' || g.toLowerCase() === 'animation')) {
                                displayType = 'anime';
                                typeLabel = self.t('typeAnime');
                            } else {
                                displayType = 'series';
                                typeLabel = self.t('typeSeries');
                            }
                        }

                        // Badge for new episodes vs new media
                        let badge = '';
                        if (!item.isNewMedia && item.newEpisodeCount > 0) {
                            const epText = item.newEpisodeCount === 1
                                ? (self.t('newEpisode') || '+1 episode')
                                : (self.t('newEpisodes') || `+${item.newEpisodeCount} episodes`).replace('{count}', item.newEpisodeCount);
                            badge = `<span class="latest-item-badge new-episodes">${epText}</span>`;
                        }

                        // Add "NEW" badge for items less than 2 hours old
                        const itemAge = Date.now() - new Date(item.DateCreated).getTime();
                        const twoHoursMs = 2 * 60 * 60 * 1000;
                        if (itemAge < twoHoursMs) {
                            badge += `<span class="latest-item-badge is-new">NEW</span>`;
                        }

                        // Get image URL
                        const imageSrc = item.ImageTags && item.ImageTags.Primary
                            ? `${baseUrl}/Items/${itemId}/Images/Primary?maxHeight=96&tag=${item.ImageTags.Primary}`
                            : `${baseUrl}/Items/${itemId}/Images/Primary?maxHeight=96`;

                        html += `
                            <a href="#!/details?id=${itemId}" class="latest-item" data-item-id="${itemId}">
                                <img src="${imageSrc}" class="latest-item-image" alt="" onerror="this.style.visibility='hidden'"/>
                                <div class="latest-item-info">
                                    <div class="latest-item-title">${self.escapeHtml(itemName)}${badge}</div>
                                    <div class="latest-item-meta">
                                        <span class="latest-item-type ${displayType}">${typeLabel}</span>
                                        ${itemYear ? `<span class="latest-item-year">${itemYear}</span>` : ''}
                                        ${timeAgo ? `<span class="latest-item-time">${timeAgo}</span>` : ''}
                                    </div>
                                </div>
                            </a>
                        `;
                    });

                    dropdown.innerHTML = html;

                    // Add click handlers to close dropdown after navigation
                    dropdown.querySelectorAll('.latest-item').forEach(item => {
                        item.addEventListener('click', () => {
                            dropdown.classList.remove('visible');
                        });
                    });
                });
            })
            .catch(err => {
                console.error('Failed to load latest media:', err);
                dropdown.innerHTML = `<div class="latest-header">${self.t('latestMedia')}</div><div class="latest-empty">${self.t('latestMediaError')}</div>`;
            });
        },

        // ===============================================
        // Media Management Functions (Admin Only)
        // ===============================================

        /**
         * Initialize media management button (replaces original search button, admin only)
         * Follows same pattern as initLatestMediaButton
         */
        initMediaManagementButtonWithRetry: function () {
            const self = this;
            try {
                // Check if already exists
                if (document.getElementById('mediaManagementBtn')) {
                    return;
                }

                // Check config and admin status
                const checkConfigAndCreate = () => {
                    if (!window.ApiClient) {
                        setTimeout(checkConfigAndCreate, 1000);
                        return;
                    }
                    const baseUrl = ApiClient.serverAddress();
                    fetch(`${baseUrl}/Ratings/Config`, { method: 'GET', credentials: 'include' })
                        .then(response => response.json())
                        .then(config => {
                            if (config.EnableMediaManagement === false) {
                                return; // Don't create button
                            }
                            // Check if admin
                            self.checkIfAdmin().then(isAdmin => {
                                if (isAdmin) {
                                    createMediaManagementButton();
                                }
                            });
                        })
                        .catch((err) => {
                            // Default to checking admin status
                            self.checkIfAdmin().then(isAdmin => {
                                if (isAdmin) {
                                    createMediaManagementButton();
                                }
                            });
                        });
                };

                const createMediaManagementButton = () => {
                    try {
                        // Check if already exists
                        if (document.getElementById('mediaManagementBtn')) {
                            return;
                        }

                        // Create modal/dialog elements first
                        self.initMediaManagementButton();

                        // Find and hide the original search button
                        const searchBtn = document.querySelector('.headerSearchButton');
                        if (searchBtn) {
                            searchBtn.style.display = 'none';
                        }

                        // Create the media management button
                        const btn = document.createElement('button');
                        btn.id = 'mediaManagementBtn';
                        btn.className = 'headerButton headerButtonRight paper-icon-button-light';
                        btn.setAttribute('type', 'button');
                        btn.setAttribute('title', self.t('mediaManagement'));
                        // Folder icon for media management
                        btn.innerHTML = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="currentColor">
                            <path d="M20 6h-8l-2-2H4c-1.1 0-1.99.9-1.99 2L2 18c0 1.1.9 2 2 2h16c1.1 0 2-.9 2-2V8c0-1.1-.9-2-2-2zm0 12H4V8h16v10z"/>
                        </svg>`;

                        // Click handler to open modal
                        btn.addEventListener('click', (e) => {
                            e.preventDefault();
                            e.stopPropagation();
                            self.openMediaManagementModal();
                        });

                        // Insert button in header - try to find headerRight or similar container
                        const headerRight = document.querySelector('.headerRight');
                        if (headerRight) {
                            // Insert at the beginning of headerRight
                            headerRight.insertBefore(btn, headerRight.firstChild);
                        } else {
                            // Fallback: find skinHeader and append
                            const skinHeader = document.querySelector('.skinHeader');
                            if (skinHeader) {
                                skinHeader.appendChild(btn);
                            } else {
                                document.body.appendChild(btn);
                            }
                        }

                        // Observe for search button appearing later (SPA navigation)
                        const observer = new MutationObserver(() => {
                            const searchButton = document.querySelector('.headerSearchButton');
                            if (searchButton && searchButton.style.display !== 'none') {
                                searchButton.style.display = 'none';
                            }
                        });
                        observer.observe(document.body, { childList: true, subtree: true });

                        // Hide during video playback and on login page
                        setInterval(() => {
                            try {
                                const videoPlayer = document.querySelector('.videoPlayerContainer');
                                const isVideoPlaying = videoPlayer && !videoPlayer.classList.contains('hide');
                                const isLoginPage = self.isOnLoginPage();

                                if (isVideoPlaying || isLoginPage) {
                                    btn.classList.add('hidden');
                                } else {
                                    btn.classList.remove('hidden');
                                }
                            } catch (err) {
                                // Silently fail
                            }
                        }, 1000);

                    } catch (err) {
                        console.error('Error creating media management button:', err);
                    }
                };

                // Try to create after a delay (same timing as Latest Media button)
                setTimeout(checkConfigAndCreate, 1700);

                // Also try on page visibility change
                document.addEventListener('visibilitychange', () => {
                    if (document.visibilityState === 'visible' && !document.getElementById('mediaManagementBtn')) {
                        setTimeout(checkConfigAndCreate, 500);
                    }
                });

            } catch (err) {
                console.error('Error initializing media management button:', err);
            }
        },

        /**
         * Initialize Media Management Button - creates modal and dialog elements
         */
        initMediaManagementButton: function () {
            const self = this;

            // Create modal if not exists
            if (!document.getElementById('mediaManagementModal')) {
                    const modal = document.createElement('div');
                    modal.id = 'mediaManagementModal';
                    modal.innerHTML = `
                        <div id="mediaManagementModalContent">
                            <button id="mediaManagementModalClose" type="button">&times;</button>
                            <div id="mediaManagementModalTitle">${self.t('mediaManagementTitle')}</div>
                            <div id="mediaManagementTabs"></div>
                            <div id="mediaManagementControls">
                                <input type="text" id="mediaSearchInput" placeholder="${self.t('mediaSearch')}" />
                                <select id="mediaSortBy">
                                    <option value="dateAdded">${self.t('mediaSortDateAdded')}</option>
                                    <option value="title">${self.t('mediaSortTitle')}</option>
                                    <option value="year">${self.t('mediaSortYear')}</option>
                                    <option value="rating">${self.t('mediaSortRating')}</option>
                                    <option value="playcount">${self.t('mediaSortPlays')}</option>
                                    <option value="size">${self.t('mediaSortSize')}</option>
                                </select>
                                <select id="mediaSortOrder">
                                    <option value="desc">↓</option>
                                    <option value="asc">↑</option>
                                </select>
                            </div>
                            <div id="mediaManagementSettings" style="display: none;">
                                <div class="settings-section">
                                    <h3>${self.t('mediaIncludeTypes')}</h3>
                                    <p style="color: #888; font-size: 12px; margin-bottom: 10px;">${self.t('mediaTypesHint')}</p>
                                    <div id="mediaTypeCheckboxes"></div>
                                </div>
                            </div>
                            <div id="mediaManagementBody">
                                <p style="text-align: center; color: #999; padding: 20px;">${self.t('mediaLoading')}</p>
                            </div>
                            <div id="mediaManagementPagination"></div>
                        </div>
                    `;
                    document.body.appendChild(modal);

                    // Close button
                    document.getElementById('mediaManagementModalClose').addEventListener('click', (e) => {
                        e.preventDefault();
                        modal.classList.remove('show');
                        document.body.style.overflow = '';
                    });

                    // Click outside to close
                    modal.addEventListener('click', (e) => {
                        if (e.target === modal) {
                            modal.classList.remove('show');
                            document.body.style.overflow = '';
                        }
                    });

                    // Build tabs dynamically and bind handlers
                    self.buildMediaTabs();

                    // Filter/sort change handlers - only trigger for media tabs, not scheduled/settings
                    let searchTimeout;
                    const triggerMediaLoad = () => {
                        if (self.mediaListState.currentTab !== 'scheduled' && self.mediaListState.currentTab !== 'settings') {
                            self.loadMediaList();
                        }
                    };
                    document.getElementById('mediaSearchInput').addEventListener('input', () => {
                        clearTimeout(searchTimeout);
                        searchTimeout = setTimeout(triggerMediaLoad, 500);
                    });
                    document.getElementById('mediaSortBy').addEventListener('change', triggerMediaLoad);
                    document.getElementById('mediaSortOrder').addEventListener('change', triggerMediaLoad);
                }

                // Create deletion dialog if not exists
                if (!document.getElementById('deletionDialog')) {
                    const deletionDialog = document.createElement('div');
                    deletionDialog.id = 'deletionDialog';
                    deletionDialog.innerHTML = `
                        <div id="deletionDialogContent">
                            <button class="deletion-close-btn" title="Close">×</button>
                            <div id="deletionDialogTitle">${self.t('mediaScheduleDelete')}</div>
                            <div id="deletionDialogOptions">
                                <button class="deletion-option-btn" data-days="1">${self.t('media1Day')}</button>
                                <button class="deletion-option-btn" data-days="3">${self.t('media3Days')}</button>
                                <button class="deletion-option-btn" data-days="7">${self.t('media1Week')}</button>
                                <button class="deletion-option-btn" data-days="14">${self.t('media2Weeks')}</button>
                            </div>
                            <div id="deletionDialogCustom">
                                <input type="number" id="deletionCustomHours" min="1" max="8760" placeholder="${self.t('mediaCustomHours') || 'Hours'}" />
                                <button class="deletion-custom-btn">${self.t('mediaSchedule') || 'Schedule'}</button>
                            </div>
                            <button class="deletion-cancel-btn">${self.t('mediaCancel') || 'Cancel'}</button>
                        </div>
                    `;
                    document.body.appendChild(deletionDialog);

                    // Close button click
                    deletionDialog.querySelector('.deletion-close-btn').addEventListener('click', () => {
                        deletionDialog.classList.remove('show');
                    });

                    // Click outside to close
                    deletionDialog.addEventListener('click', (e) => {
                        if (e.target === deletionDialog) {
                            deletionDialog.classList.remove('show');
                        }
                    });

                    // Deletion dialog option clicks
                    deletionDialog.querySelectorAll('.deletion-option-btn').forEach(btn => {
                        btn.addEventListener('click', () => {
                            const days = parseInt(btn.getAttribute('data-days'));
                            const itemId = deletionDialog.getAttribute('data-item-id');
                            if (itemId && days) {
                                self.scheduleDeletion(itemId, days);
                            }
                            deletionDialog.classList.remove('show');
                        });
                    });

                    // Custom hours button
                    deletionDialog.querySelector('.deletion-custom-btn').addEventListener('click', () => {
                        const hours = parseInt(document.getElementById('deletionCustomHours').value);
                        const itemId = deletionDialog.getAttribute('data-item-id');
                        if (itemId && hours && hours > 0) {
                            self.scheduleDeletionHours(itemId, hours);
                            deletionDialog.classList.remove('show');
                        }
                    });

                    deletionDialog.querySelector('.deletion-cancel-btn').addEventListener('click', () => {
                        deletionDialog.classList.remove('show');
                    });
                }
        },

        /**
         * Open Media Management Modal
         */
        openMediaManagementModal: function () {
            const modal = document.getElementById('mediaManagementModal');
            if (modal) {
                modal.classList.add('show');
                document.body.style.overflow = 'hidden';
                // Set current tab to 'all' (default when opening)
                this.mediaListState.currentTab = 'all';
                this.loadMediaList();
            }
        },

        /**
         * Current media list state
         */
        mediaListState: {
            page: 1,
            totalPages: 1,
            requestId: 0,
            currentTab: 'all' // 'all', 'scheduled', 'settings', or specific type
        },

        /**
         * Load media list from API
         */
        loadMediaList: function (page) {
            const self = this;
            const body = document.getElementById('mediaManagementBody');
            const pagination = document.getElementById('mediaManagementPagination');

            if (!body) return;

            // Increment request ID to track this specific request
            const thisRequestId = ++self.mediaListState.requestId;
            const currentPage = page || 1;
            const search = document.getElementById('mediaSearchInput')?.value || '';
            const activeTab = document.querySelector('#mediaManagementTabs .media-tab.active');
            const type = activeTab?.getAttribute('data-type') || '';
            const sortBy = document.getElementById('mediaSortBy')?.value || 'dateAdded';
            const sortOrder = document.getElementById('mediaSortOrder')?.value || 'desc';

            body.innerHTML = `<p style="text-align: center; color: #999; padding: 20px;">${self.t('mediaLoading')}</p>`;

            const baseUrl = ApiClient.serverAddress();
            const userId = ApiClient.getCurrentUserId();
            const token = ApiClient.accessToken();
            const batchSize = 10; // Load 10 items at a time

            // Progressive loading - load in batches
            const loadBatch = async (batchPage, isFirst) => {
                let url = `${baseUrl}/Ratings/Media?page=${batchPage}&pageSize=${batchSize}&sortBy=${sortBy}&sortOrder=${sortOrder}`;
                if (search) url += `&search=${encodeURIComponent(search)}`;
                // Handle library-based types (e.g., library_abc123 for Anime library)
                if (type && type.startsWith('library_')) {
                    const libraryId = type.replace('library_', '');
                    url += `&parentId=${encodeURIComponent(libraryId)}`;
                } else if (type) {
                    url += `&type=${encodeURIComponent(type)}`;
                }

                const response = await fetch(url, {
                    method: 'GET',
                    headers: {
                        'X-Emby-Authorization': `MediaBrowser Client="Jellyfin Web", Device="Browser", DeviceId="Ratings", Version="1.0", Token="${token}"`
                    },
                    credentials: 'include',
                    cache: 'no-store'
                });

                if (!response.ok) throw new Error('Failed to load media');
                return response.json();
            };

            // Calculate batch range for requested page (50 items per page = 5 batches of 10)
            const batchesPerPage = 5;
            const startBatch = (currentPage - 1) * batchesPerPage + 1;
            const endBatch = currentPage * batchesPerPage;

            // Start loading first batch of the page immediately
            loadBatch(startBatch, true)
                .then(async (firstData) => {
                    // Check if this request is still current (prevents race conditions)
                    if (thisRequestId !== self.mediaListState.requestId) {
                        return;
                    }

                    // Check if user switched to a different tab type
                    if (self.mediaListState.currentTab === 'scheduled' || self.mediaListState.currentTab === 'settings') {
                        return;
                    }

                    self.mediaListState.page = currentPage;
                    self.mediaListState.totalPages = Math.ceil(firstData.TotalItems / 50); // Actual page count for 50 items/page

                    // Render table with first batch
                    self.renderMediaTable(firstData.Items, body, 0);
                    self.renderPagination(pagination, {
                        CurrentPage: currentPage,
                        TotalPages: self.mediaListState.totalPages,
                        TotalItems: firstData.TotalItems
                    });

                    // Calculate how many more batches we need for this page
                    const totalBatchesNeeded = Math.ceil(firstData.TotalItems / batchSize);
                    const lastBatchForPage = Math.min(endBatch, totalBatchesNeeded);

                    // Load remaining batches for this page
                    for (let batch = startBatch + 1; batch <= lastBatchForPage; batch++) {
                        // Check again before each batch
                        if (thisRequestId !== self.mediaListState.requestId) {
                            return;
                        }
                        try {
                            const batchData = await loadBatch(batch, false);
                            // Check again after await - tab may have changed during network request
                            if (thisRequestId !== self.mediaListState.requestId) {
                                return;
                            }
                            if (batchData.Items && batchData.Items.length > 0) {
                                self.appendMediaRows(batchData.Items, body, (batch - startBatch) * batchSize);
                            }
                        } catch (err) {
                            console.error('Error loading batch:', err);
                        }
                    }
                })
                .catch(err => {
                    // Only show error if this is still the current request
                    if (thisRequestId === self.mediaListState.requestId) {
                        console.error('Error loading media:', err);
                        body.innerHTML = `<p style="text-align: center; color: #e74c3c; padding: 20px;">${self.t('mediaError')}</p>`;
                    }
                });
        },

        /**
         * Append media rows to existing table
         */
        appendMediaRows: function (items, container, startIndex) {
            const self = this;
            const tbody = container.querySelector('tbody');
            if (!tbody || !items || items.length === 0) return;

            const baseUrl = ApiClient.serverAddress();

            const formatSize = (bytes) => {
                if (!bytes || bytes === 0) return '-';
                const gb = bytes / (1024 * 1024 * 1024);
                if (gb >= 1) return gb.toFixed(1) + ' ' + self.t('mediaGB');
                const mb = bytes / (1024 * 1024);
                return mb.toFixed(0) + ' ' + self.t('mediaMB');
            };

            const formatDaysUntil = (deleteAt) => {
                const now = new Date();
                const deleteDate = new Date(deleteAt);
                const diffMs = deleteDate - now;
                const diffDays = Math.ceil(diffMs / (1000 * 60 * 60 * 24));
                if (diffDays <= 0) return 'Today';
                return diffDays + ' ' + self.t('mediaDays');
            };

            items.forEach((item, index) => {
                const imageUrl = item.ImageUrl ? baseUrl + item.ImageUrl : '';
                const hasScheduledDeletion = item.ScheduledDeletion && !item.ScheduledDeletion.IsCancelled;
                const playCountDisplay = item.PlayCount > 0 ? item.PlayCount.toLocaleString() : '-';
                const animDelay = (startIndex + index) * 60;

                const tr = document.createElement('tr');
                tr.style.animationDelay = `${animDelay}ms`;
                tr.innerHTML = `
                    <td>
                        ${imageUrl ? `<img src="${imageUrl}?maxWidth=80" class="media-item-image" alt="" />` : '<div class="media-item-image"></div>'}
                    </td>
                    <td>
                        <div class="media-item-title">
                            <a href="#/details?id=${item.ItemId}">${item.Title}</a>
                        </div>
                        <span class="media-item-type ${item.Type.toLowerCase()}">${item.Type}</span>
                    </td>
                    <td>${item.Year || '-'}</td>
                    <td class="media-item-rating">${item.AverageRating ? '★ ' + item.AverageRating.toFixed(1) : '-'}</td>
                    <td class="media-item-plays">${playCountDisplay}</td>
                    <td>${formatSize(item.FileSizeBytes)}</td>
                    <td>
                        ${hasScheduledDeletion
                            ? `<span class="media-item-scheduled">${self.t('mediaLeavingIn')} ${formatDaysUntil(item.ScheduledDeletion.DeleteAt)}</span>`
                            : ''}
                    </td>
                    <td class="media-actions">
                        ${hasScheduledDeletion
                            ? `<button class="media-action-btn cancel" data-item-id="${item.ItemId}" data-action="cancel">${self.t('mediaCancelDelete')}</button>`
                            : `<button class="media-action-btn delete" data-item-id="${item.ItemId}" data-action="delete">${self.t('mediaScheduleDelete')}</button>`
                        }
                    </td>
                `;
                tbody.appendChild(tr);

                // Add click handler
                tr.querySelector('.media-action-btn')?.addEventListener('click', function() {
                    const itemId = this.getAttribute('data-item-id');
                    const action = this.getAttribute('data-action');
                    if (action === 'delete') {
                        self.showDeletionDialog(itemId);
                    } else if (action === 'cancel') {
                        self.cancelDeletion(itemId);
                    }
                });
            });
        },

        /**
         * Render media table
         */
        renderMediaTable: function (items, container, startIndex) {
            const self = this;
            startIndex = startIndex || 0;

            if (!items || items.length === 0) {
                container.innerHTML = `<p style="text-align: center; color: #999; padding: 20px;">${self.t('mediaNoResults')}</p>`;
                return;
            }

            const baseUrl = ApiClient.serverAddress();

            // Format file size
            const formatSize = (bytes) => {
                if (!bytes || bytes === 0) return '-';
                const gb = bytes / (1024 * 1024 * 1024);
                if (gb >= 1) return gb.toFixed(1) + ' ' + self.t('mediaGB');
                const mb = bytes / (1024 * 1024);
                return mb.toFixed(0) + ' ' + self.t('mediaMB');
            };

            // Format days until deletion
            const formatDaysUntil = (deleteAt) => {
                const now = new Date();
                const deleteDate = new Date(deleteAt);
                const diffMs = deleteDate - now;
                const diffDays = Math.ceil(diffMs / (1000 * 60 * 60 * 24));
                if (diffDays <= 0) return 'Today';
                return diffDays + ' ' + self.t('mediaDays');
            };

            let html = `
                <table class="media-list-table">
                    <thead>
                        <tr>
                            <th></th>
                            <th>${self.t('mediaSortTitle')}</th>
                            <th>${self.t('mediaSortYear')}</th>
                            <th>${self.t('mediaSortRating')}</th>
                            <th>${self.t('mediaSortPlayCount')}</th>
                            <th>${self.t('mediaSortSize')}</th>
                            <th>Status</th>
                            <th>Actions</th>
                        </tr>
                    </thead>
                    <tbody>
            `;

            items.forEach((item, index) => {
                const imageUrl = item.ImageUrl ? baseUrl + item.ImageUrl : '';
                const hasScheduledDeletion = item.ScheduledDeletion && !item.ScheduledDeletion.IsCancelled;

                // Format play count
                const playCountDisplay = item.PlayCount > 0 ? item.PlayCount.toLocaleString() : '-';

                // Staggered animation delay for each row
                const animDelay = (startIndex + index) * 60;

                html += `
                    <tr style="animation-delay: ${animDelay}ms">
                        <td>
                            ${imageUrl ? `<img src="${imageUrl}?maxWidth=80" class="media-item-image" alt="" />` : '<div class="media-item-image"></div>'}
                        </td>
                        <td>
                            <div class="media-item-title">
                                <a href="#/details?id=${item.ItemId}">${item.Title}</a>
                            </div>
                            <span class="media-item-type ${item.Type.toLowerCase()}">${item.Type}</span>
                        </td>
                        <td>${item.Year || '-'}</td>
                        <td class="media-item-rating">${item.AverageRating ? '★ ' + item.AverageRating.toFixed(1) : '-'}</td>
                        <td class="media-item-plays">${playCountDisplay}</td>
                        <td>${formatSize(item.FileSizeBytes)}</td>
                        <td>
                            ${hasScheduledDeletion
                                ? `<span class="media-item-scheduled">${self.t('mediaLeavingIn')} ${formatDaysUntil(item.ScheduledDeletion.DeleteAt)}</span>`
                                : ''}
                        </td>
                        <td class="media-actions">
                            ${hasScheduledDeletion
                                ? `<button class="media-action-btn cancel" data-item-id="${item.ItemId}" data-action="cancel">${self.t('mediaCancelDelete')}</button>`
                                : `<button class="media-action-btn delete" data-item-id="${item.ItemId}" data-action="delete">${self.t('mediaScheduleDelete')}</button>`
                            }
                        </td>
                    </tr>
                `;
            });

            html += `</tbody></table>`;
            container.innerHTML = html;

            // Add action button handlers
            container.querySelectorAll('.media-action-btn').forEach(btn => {
                btn.addEventListener('click', () => {
                    const itemId = btn.getAttribute('data-item-id');
                    const action = btn.getAttribute('data-action');
                    if (action === 'delete') {
                        self.showDeletionDialog(itemId);
                    } else if (action === 'cancel') {
                        self.cancelDeletion(itemId);
                    }
                });
            });
        },

        /**
         * Render pagination controls
         */
        renderPagination: function (container, data) {
            const self = this;
            if (!container) return;

            const { CurrentPage, TotalPages, TotalItems } = data;

            container.innerHTML = `
                <div class="pagination-wrapper">
                    <button class="pagination-nav-btn" ${CurrentPage <= 1 ? 'disabled' : ''} data-page="${CurrentPage - 1}">
                        <span class="pagination-arrow">‹</span> ${self.t('mediaPrev')}
                    </button>
                    <div class="pagination-center">
                        <span class="pagination-label">${self.t('mediaPage')}</span>
                        <input type="number" id="mediaPageInput" value="${CurrentPage}" min="1" max="${TotalPages}">
                        <button class="pagination-go-btn" id="mediaPageGoBtn">${self.t('mediaGo')}</button>
                        <span class="pagination-info">${self.t('mediaOf')} ${TotalPages} <span class="pagination-items">(${TotalItems})</span></span>
                    </div>
                    <button class="pagination-nav-btn" ${CurrentPage >= TotalPages ? 'disabled' : ''} data-page="${CurrentPage + 1}">
                        ${self.t('mediaNext')} <span class="pagination-arrow">›</span>
                    </button>
                </div>
            `;

            // Add click handlers for prev/next buttons
            container.querySelectorAll('button.pagination-nav-btn').forEach(btn => {
                btn.addEventListener('click', () => {
                    const page = parseInt(btn.getAttribute('data-page'));
                    if (page >= 1 && page <= TotalPages) {
                        self.loadMediaList(page);
                    }
                });
            });

            // Add handler for Go button
            const goBtn = document.getElementById('mediaPageGoBtn');
            const pageInput = document.getElementById('mediaPageInput');

            if (goBtn && pageInput) {
                goBtn.addEventListener('click', () => {
                    const page = parseInt(pageInput.value);
                    if (page >= 1 && page <= TotalPages) {
                        self.loadMediaList(page);
                    } else {
                        pageInput.value = CurrentPage;
                    }
                });
            }
        },

        /**
         * Show deletion dialog
         */
        showDeletionDialog: function (itemId) {
            const dialog = document.getElementById('deletionDialog');
            if (dialog) {
                dialog.setAttribute('data-item-id', itemId);
                dialog.classList.add('show');
            }
        },

        /**
         * Schedule deletion for an item
         */
        scheduleDeletionHours: function (itemId, hours) {
            const self = this;
            const baseUrl = ApiClient.serverAddress();
            const token = ApiClient.accessToken();

            fetch(`${baseUrl}/Ratings/Media/${itemId}/ScheduleDeletion?delayHours=${hours}`, {
                method: 'POST',
                headers: {
                    'X-Emby-Authorization': `MediaBrowser Client="Jellyfin Web", Device="Browser", DeviceId="Ratings", Version="1.0", Token="${token}"`
                },
                credentials: 'include'
            })
            .then(response => {
                if (!response.ok) throw new Error('Failed to schedule deletion');
                return response.json();
            })
            .then(() => {
                self.loadMediaList(self.mediaListState.page);
                self.loadScheduledDeletions();
            })
            .catch(err => {
                console.error('Error scheduling deletion:', err);
                alert('Failed to schedule deletion');
            });
        },

        scheduleDeletion: function (itemId, delayDays) {
            const self = this;
            const baseUrl = ApiClient.serverAddress();
            const token = ApiClient.accessToken();

            fetch(`${baseUrl}/Ratings/Media/${itemId}/ScheduleDeletion?delayDays=${delayDays}`, {
                method: 'POST',
                headers: {
                    'X-Emby-Authorization': `MediaBrowser Client="Jellyfin Web", Device="Browser", DeviceId="Ratings", Version="1.0", Token="${token}"`
                },
                credentials: 'include'
            })
            .then(response => {
                if (!response.ok) throw new Error('Failed to schedule deletion');
                return response.json();
            })
            .then(() => {
                // Reload list and refresh badges
                self.loadMediaList(self.mediaListState.page);
                self.loadScheduledDeletions();
            })
            .catch(err => {
                console.error('Error scheduling deletion:', err);
                alert('Failed to schedule deletion');
            });
        },

        /**
         * Cancel scheduled deletion for an item
         */
        cancelDeletion: function (itemId) {
            const self = this;
            const baseUrl = ApiClient.serverAddress();
            const token = ApiClient.accessToken();

            fetch(`${baseUrl}/Ratings/Media/${itemId}/ScheduleDeletion`, {
                method: 'DELETE',
                headers: {
                    'X-Emby-Authorization': `MediaBrowser Client="Jellyfin Web", Device="Browser", DeviceId="Ratings", Version="1.0", Token="${token}"`
                },
                credentials: 'include'
            })
            .then(response => {
                if (!response.ok) throw new Error('Failed to cancel deletion');
                return response.json();
            })
            .then(() => {
                // Reload list and refresh badges
                self.loadMediaList(self.mediaListState.page);
                self.loadScheduledDeletions();
            })
            .catch(err => {
                console.error('Error cancelling deletion:', err);
                alert('Failed to cancel deletion');
            });
        },

        /**
         * Load scheduled media list (items scheduled for deletion)
         */
        loadScheduledMediaList: function () {
            const self = this;
            const body = document.getElementById('mediaManagementBody');
            const pagination = document.getElementById('mediaManagementPagination');

            if (!body) return;

            // Increment request ID to cancel any pending loadMediaList
            const thisRequestId = ++self.mediaListState.requestId;

            body.innerHTML = `<p style="text-align: center; color: #999; padding: 20px;">${self.t('mediaLoading')}</p>`;
            pagination.style.display = 'none';
            pagination.innerHTML = '';

            const baseUrl = ApiClient.serverAddress();
            const token = ApiClient.accessToken();

            fetch(`${baseUrl}/Ratings/ScheduledDeletions`, {
                method: 'GET',
                headers: {
                    'X-Emby-Authorization': `MediaBrowser Client="Jellyfin Web", Device="Browser", DeviceId="Ratings", Version="1.0", Token="${token}"`
                },
                credentials: 'include'
            })
            .then(response => {
                if (!response.ok) throw new Error('Failed to load scheduled deletions');
                return response.json();
            })
            .then(deletions => {
                // Check if this request is still current
                if (thisRequestId !== self.mediaListState.requestId) {
                    return;
                }

                // Check if user switched to a different tab
                if (self.mediaListState.currentTab !== 'scheduled') {
                    return;
                }

                if (!deletions || deletions.length === 0) {
                    body.innerHTML = `<p style="text-align: center; color: #999; padding: 40px;">${self.t('mediaNoScheduled') || 'No scheduled deletions'}</p>`;
                    return;
                }

                // Format time remaining
                const formatTimeLeft = (deleteAt) => {
                    const now = new Date();
                    const deleteDate = new Date(deleteAt);
                    const diffMs = deleteDate - now;
                    const diffHours = Math.floor(diffMs / (1000 * 60 * 60));
                    const diffDays = Math.floor(diffHours / 24);

                    if (diffDays > 0) {
                        return `${diffDays} ${self.t('mediaDays')}`;
                    } else if (diffHours > 0) {
                        return `${diffHours}h`;
                    } else {
                        return self.t('mediaSoon') || 'Soon';
                    }
                };

                // Build table matching regular media style
                let html = `
                    <table class="media-list-table">
                        <thead>
                            <tr>
                                <th></th>
                                <th>${self.t('mediaSortTitle')}</th>
                                <th>${self.t('mediaScheduledBy') || 'Scheduled By'}</th>
                                <th>${self.t('mediaDeletesIn') || 'Deletes In'}</th>
                                <th>${self.t('mediaActions') || 'Actions'}</th>
                            </tr>
                        </thead>
                        <tbody>
                `;

                deletions.forEach((item, index) => {
                    const imageUrl = `/Items/${item.ItemId}/Images/Primary`;
                    const deleteDate = new Date(item.DeleteAt);
                    const now = new Date();
                    const diffMs = deleteDate - now;
                    const diffHours = Math.floor(diffMs / (1000 * 60 * 60));
                    const diffDays = Math.floor(diffHours / 24);
                    const animDelay = index * 60;

                    // Color based on urgency
                    let urgencyColor = '#27ae60'; // green
                    if (diffDays <= 1) urgencyColor = '#e74c3c'; // red
                    else if (diffDays <= 3) urgencyColor = '#f39c12'; // orange

                    html += `
                        <tr style="animation-delay: ${animDelay}ms">
                            <td>
                                <img src="${baseUrl}${imageUrl}?maxWidth=80" class="media-item-image" alt="" onerror="this.style.display='none'" />
                            </td>
                            <td>
                                <div class="media-item-title">
                                    <a href="#/details?id=${item.ItemId}">${self.escapeHtml(item.ItemTitle)}</a>
                                </div>
                                <span class="media-item-type ${item.ItemType.toLowerCase()}">${item.ItemType}</span>
                            </td>
                            <td style="color: #888;">${self.escapeHtml(item.ScheduledByUsername)}</td>
                            <td>
                                <span class="scheduled-time-badge" style="background: ${urgencyColor};">
                                    ${formatTimeLeft(item.DeleteAt)}
                                </span>
                                <div style="font-size: 11px; color: #666; margin-top: 4px;">
                                    ${deleteDate.toLocaleDateString()} ${deleteDate.toLocaleTimeString([], {hour: '2-digit', minute:'2-digit'})}
                                </div>
                            </td>
                            <td class="media-actions">
                                <div class="scheduled-actions-wrapper">
                                    <button class="media-action-btn change" data-item-id="${item.ItemId}" data-action="change" title="${self.t('mediaChangeTime') || 'Change time'}">
                                        ${self.t('mediaChange') || 'Change'}
                                    </button>
                                    <button class="media-action-btn cancel" data-item-id="${item.ItemId}" data-action="cancel" title="${self.t('mediaCancelDelete')}">
                                        ${self.t('mediaCancel') || 'Cancel'}
                                    </button>
                                </div>
                            </td>
                        </tr>
                    `;
                });

                html += '</tbody></table>';
                body.innerHTML = html;

                // Add event handlers for action buttons
                body.querySelectorAll('.media-action-btn').forEach(btn => {
                    btn.addEventListener('click', () => {
                        const itemId = btn.getAttribute('data-item-id');
                        const action = btn.getAttribute('data-action');

                        if (action === 'cancel') {
                            self.cancelDeletion(itemId);
                            setTimeout(() => self.loadScheduledMediaList(), 500);
                        } else if (action === 'change') {
                            self.showDeletionDialog(itemId);
                        }
                    });
                });
            })
            .catch(err => {
                if (thisRequestId === self.mediaListState.requestId) {
                    console.error('Error loading scheduled media:', err);
                    body.innerHTML = `<p style="text-align: center; color: #e74c3c; padding: 20px;">${self.t('mediaError')}</p>`;
                }
            });
        },

        /**
         * Available media types from server
         */
        availableMediaTypes: null,

        /**
         * Selected media types for tabs
         */
        selectedMediaTypes: ['Movie', 'Series'],

        /**
         * Build media tabs dynamically based on selected types
         */
        buildMediaTabs: function () {
            const self = this;
            const tabsContainer = document.getElementById('mediaManagementTabs');
            if (!tabsContainer) return;

            // Load saved settings
            const saved = localStorage.getItem('ratingsMediaTypes');
            if (saved) {
                try {
                    self.selectedMediaTypes = JSON.parse(saved);
                } catch (e) {
                    self.selectedMediaTypes = ['Movie', 'Series'];
                }
            }

            // Get labels for types
            const typeLabels = {
                'Movie': self.t('mediaTypeMovie'),
                'Series': self.t('mediaTypeSeries'),
                'MusicAlbum': 'Music',
                'MusicVideo': 'Music Videos',
                'Video': 'Home Videos',
                'BoxSet': 'Collections',
                'Book': 'Books',
                'Photo': 'Photos'
            };

            // Load saved labels from localStorage (for library-based types like Anime)
            const savedLabels = JSON.parse(localStorage.getItem('ratingsMediaTypeLabels') || '{}');
            Object.assign(typeLabels, savedLabels);

            // Check availableMediaTypes for custom labels (like Anime)
            if (self.availableMediaTypes) {
                self.availableMediaTypes.forEach(mt => {
                    if (mt.label && mt.type) {
                        typeLabels[mt.type] = mt.label;
                    }
                    // Handle library-based types like Anime
                    if (mt.libraryId) {
                        typeLabels['library_' + mt.libraryId] = mt.label;
                    }
                });
            }

            // Check if any library types are missing labels - if so, fetch types
            const hasUnlabeledLibraryTypes = self.selectedMediaTypes.some(type =>
                type.startsWith('library_') && !typeLabels[type]
            );
            if (hasUnlabeledLibraryTypes && !self.availableMediaTypes) {
                // Fetch available types to get labels, then rebuild tabs
                self.fetchAvailableMediaTypes().then(() => {
                    self.buildMediaTabs();
                });
                return; // Exit now, will be called again after fetch
            }

            // Build tabs HTML
            let html = `<button class="media-tab active" data-type="">${self.t('mediaTypeAll')}</button>`;

            // Add tabs for each selected media type
            self.selectedMediaTypes.forEach(type => {
                const label = typeLabels[type] || type;
                html += `<button class="media-tab" data-type="${type}">${label}</button>`;
            });

            // Always add Scheduled and Settings tabs at the end
            html += `<button class="media-tab" data-type="scheduled">${self.t('mediaTypeScheduled')}</button>`;
            html += `<button class="media-tab media-settings-tab" data-type="settings" title="${self.t('mediaSettings')}">⚙</button>`;

            tabsContainer.innerHTML = html;

            // Bind click handlers
            tabsContainer.querySelectorAll('.media-tab').forEach(tab => {
                tab.addEventListener('click', () => {
                    const tabType = tab.getAttribute('data-type');
                    tabsContainer.querySelectorAll('.media-tab').forEach(t => t.classList.remove('active'));
                    tab.classList.add('active');

                    // Track current tab to prevent race conditions
                    self.mediaListState.currentTab = tabType || 'all';

                    // Show/hide controls and settings based on tab
                    const controls = document.getElementById('mediaManagementControls');
                    const settings = document.getElementById('mediaManagementSettings');
                    const body = document.getElementById('mediaManagementBody');
                    const pagination = document.getElementById('mediaManagementPagination');

                    if (tabType === 'settings') {
                        controls.style.display = 'none';
                        settings.style.display = 'block';
                        body.style.display = 'none';
                        pagination.style.display = 'none';
                        self.loadMediaTypeSettings();
                    } else {
                        controls.style.display = 'flex';
                        settings.style.display = 'none';
                        body.style.display = 'block';
                        pagination.style.display = 'flex';

                        if (tabType === 'scheduled') {
                            self.loadScheduledMediaList();
                        } else {
                            self.loadMediaList();
                        }
                    }
                });
            });
        },

        /**
         * Load media type settings
         */
        loadMediaTypeSettings: function () {
            const self = this;
            const container = document.getElementById('mediaTypeCheckboxes');
            if (!container) return;

            // Load saved settings
            const saved = localStorage.getItem('ratingsMediaTypes');
            if (saved) {
                try {
                    self.selectedMediaTypes = JSON.parse(saved);
                } catch (e) {
                    self.selectedMediaTypes = ['Movie', 'Series'];
                }
            }

            // If we haven't loaded available types yet, fetch from server
            if (!self.availableMediaTypes) {
                container.innerHTML = '<p style="color: #888;">Loading available types...</p>';
                self.fetchAvailableMediaTypes().then(() => {
                    self.renderMediaTypeCheckboxes(container);
                });
            } else {
                self.renderMediaTypeCheckboxes(container);
            }
        },

        /**
         * Fetch available media types from Jellyfin
         */
        fetchAvailableMediaTypes: async function () {
            const self = this;
            const baseUrl = ApiClient.serverAddress();
            const userId = ApiClient.getCurrentUserId();
            const token = ApiClient.accessToken();

            try {
                // Get user's libraries to see what types are available
                const response = await fetch(`${baseUrl}/Users/${userId}/Views`, {
                    method: 'GET',
                    headers: {
                        'X-Emby-Authorization': `MediaBrowser Client="Jellyfin Web", Device="Browser", DeviceId="Ratings", Version="1.0", Token="${token}"`
                    },
                    credentials: 'include'
                });

                if (!response.ok) throw new Error('Failed to load libraries');
                const data = await response.json();

                // Extract unique collection types and map to item types
                const typeMap = {
                    'movies': { type: 'Movie', label: 'Movies' },
                    'tvshows': { type: 'Series', label: 'Series' },
                    'music': { type: 'MusicAlbum', label: 'Music' },
                    'musicvideos': { type: 'MusicVideo', label: 'Music Videos' },
                    'homevideos': { type: 'Video', label: 'Home Videos' },
                    'boxsets': { type: 'BoxSet', label: 'Collections' },
                    'books': { type: 'Book', label: 'Books' },
                    'photos': { type: 'Photo', label: 'Photos' },
                    'mixed': { type: 'Mixed', label: 'Mixed Content' }
                };

                // Build available types from user's libraries
                self.availableMediaTypes = [];
                const seenTypes = new Set();

                // Always include Movie and Series as defaults
                self.availableMediaTypes.push({ type: 'Movie', label: 'Movies' });
                self.availableMediaTypes.push({ type: 'Series', label: 'Series' });
                seenTypes.add('Movie');
                seenTypes.add('Series');

                if (data.Items) {
                    data.Items.forEach(library => {
                        const collectionType = library.CollectionType?.toLowerCase();
                        if (collectionType && typeMap[collectionType] && !seenTypes.has(typeMap[collectionType].type)) {
                            self.availableMediaTypes.push(typeMap[collectionType]);
                            seenTypes.add(typeMap[collectionType].type);
                        }

                        // Add library-specific entries for special libraries (Anime, etc.)
                        const name = library.Name || '';
                        const nameLower = name.toLowerCase();
                        const libraryKey = 'library_' + library.Id;

                        // Check for anime library
                        if ((nameLower.includes('anime') || collectionType === 'tvshows' && nameLower.includes('anime')) && !seenTypes.has(libraryKey)) {
                            self.availableMediaTypes.push({
                                type: libraryKey,
                                label: name,
                                libraryId: library.Id
                            });
                            seenTypes.add(libraryKey);
                        }

                        // Add any library that isn't already covered by standard types
                        if (!collectionType || !typeMap[collectionType]) {
                            if (!seenTypes.has(libraryKey)) {
                                self.availableMediaTypes.push({
                                    type: libraryKey,
                                    label: name,
                                    libraryId: library.Id
                                });
                                seenTypes.add(libraryKey);
                            }
                        }
                    });
                }

                // Save all library labels to localStorage for tab building
                const savedLabels = JSON.parse(localStorage.getItem('ratingsMediaTypeLabels') || '{}');
                self.availableMediaTypes.forEach(mt => {
                    if (mt.type.startsWith('library_') && mt.label) {
                        savedLabels[mt.type] = mt.label;
                    }
                });
                localStorage.setItem('ratingsMediaTypeLabels', JSON.stringify(savedLabels));

            } catch (err) {
                console.error('Error fetching media types:', err);
                // Use defaults
                self.availableMediaTypes = [
                    { type: 'Movie', label: 'Movies' },
                    { type: 'Series', label: 'Series' }
                ];
            }
        },

        /**
         * Render media type checkboxes
         */
        renderMediaTypeCheckboxes: function (container) {
            const self = this;
            if (!container || !self.availableMediaTypes) return;

            let html = '';
            self.availableMediaTypes.forEach(typeInfo => {
                const isChecked = self.selectedMediaTypes.includes(typeInfo.type);
                html += `
                    <div class="media-type-checkbox ${isChecked ? 'checked' : ''}" data-type="${typeInfo.type}">
                        <input type="checkbox" id="mediaType_${typeInfo.type}" ${isChecked ? 'checked' : ''}>
                        <label for="mediaType_${typeInfo.type}">${typeInfo.label}</label>
                    </div>
                `;
            });

            container.innerHTML = html;

            // Add event handlers
            container.querySelectorAll('.media-type-checkbox').forEach(checkbox => {
                checkbox.addEventListener('click', (e) => {
                    if (e.target.tagName === 'INPUT') return; // Let the input handle itself

                    const input = checkbox.querySelector('input');
                    input.checked = !input.checked;
                    self.handleMediaTypeChange(checkbox, input.checked);
                });

                const input = checkbox.querySelector('input');
                input.addEventListener('change', () => {
                    self.handleMediaTypeChange(checkbox, input.checked);
                });
            });
        },

        /**
         * Handle media type checkbox change
         */
        handleMediaTypeChange: function (checkbox, isChecked) {
            const self = this;
            const type = checkbox.getAttribute('data-type');
            const label = checkbox.querySelector('label')?.textContent || type;

            if (isChecked) {
                checkbox.classList.add('checked');
                if (!self.selectedMediaTypes.includes(type)) {
                    self.selectedMediaTypes.push(type);
                }
            } else {
                checkbox.classList.remove('checked');
                self.selectedMediaTypes = self.selectedMediaTypes.filter(t => t !== type);
            }

            // Save to localStorage
            localStorage.setItem('ratingsMediaTypes', JSON.stringify(self.selectedMediaTypes));

            // Also save labels for library-based types
            const savedLabels = JSON.parse(localStorage.getItem('ratingsMediaTypeLabels') || '{}');
            if (type.startsWith('library_')) {
                if (isChecked) {
                    savedLabels[type] = label;
                } else {
                    delete savedLabels[type];
                }
                localStorage.setItem('ratingsMediaTypeLabels', JSON.stringify(savedLabels));
            }

            // Rebuild tabs to reflect changes (keep settings tab active)
            self.buildMediaTabs();
            // Re-activate settings tab since we're still in settings
            const settingsTab = document.querySelector('#mediaManagementTabs .media-tab[data-type="settings"]');
            if (settingsTab) {
                document.querySelectorAll('#mediaManagementTabs .media-tab').forEach(t => t.classList.remove('active'));
                settingsTab.classList.add('active');
            }
        },

        // ===============================================
        // Deletion Badges Functions (All Users)
        // ===============================================

        /**
         * Cached scheduled deletions
         */
        scheduledDeletionsCache: {},

        /**
         * Initialize deletion badges system
         */
        initDeletionBadges: function () {
            const self = this;

            // Load scheduled deletions initially
            setTimeout(() => {
                self.loadScheduledDeletions();
            }, 3000);

            // Refresh every 5 minutes
            setInterval(() => {
                self.loadScheduledDeletions();
            }, 5 * 60 * 1000);

            // Update badges on page changes
            setInterval(() => {
                self.updateDeletionBadges();
            }, 2000);
        },

        /**
         * Load scheduled deletions from API
         */
        loadScheduledDeletions: function () {
            const self = this;

            if (!window.ApiClient) {
                return;
            }

            const baseUrl = ApiClient.serverAddress();

            fetch(`${baseUrl}/Ratings/ScheduledDeletions`, {
                method: 'GET',
                credentials: 'include'
            })
            .then(response => response.json())
            .then(deletions => {
                // Build cache by itemId
                self.scheduledDeletionsCache = {};
                deletions.forEach(d => {
                    self.scheduledDeletionsCache[d.ItemId.toLowerCase()] = d;
                });
                // Update badges immediately
                self.updateDeletionBadges();
            })
            .catch(err => {
                console.error('RatingsPlugin: Error loading scheduled deletions:', err);
            });
        },

        /**
         * Update deletion badges on cards - refreshes Netflix view badges and detail page badge
         */
        updateDeletionBadges: function () {
            const self = this;

            // Skip if no cache yet
            if (!self.scheduledDeletionsCache) {
                return;
            }

            // Update Netflix cards (custom cards created by this plugin)
            const netflixContainers = document.querySelectorAll('.netflix-view-container, .netflix-row');
            netflixContainers.forEach(container => {
                self.applyNetflixLeavingBadges(container);
            });

            // Also check detail page
            self.updateDetailPageBadge();
        },

        /**
         * Update badge on detail page - only ONE badge before play button
         */
        updateDetailPageBadge: function () {
            const self = this;

            // Get current item ID from URL
            const match = window.location.href.match(/id=([a-f0-9-]+)/i);
            if (!match) return;

            const itemId = match[1].toLowerCase();
            const deletion = self.scheduledDeletionsCache[itemId];

            // Remove ALL existing detail badges to prevent duplicates
            document.querySelectorAll('.detail-leaving-badge').forEach(b => b.remove());

            // Also remove any card-style leaving badges from detail page elements
            document.querySelectorAll('.detailPagePrimaryContainer .has-leaving, .itemDetailPage .has-leaving').forEach(el => {
                el.classList.remove('has-leaving');
                el.removeAttribute('data-leaving');
            });

            if (deletion) {
                const formatDaysUntil = (deleteAt) => {
                    const now = new Date();
                    const deleteDate = new Date(deleteAt);
                    const diffMs = deleteDate - now;
                    const diffDays = Math.ceil(diffMs / (1000 * 60 * 60 * 24));
                    if (diffDays <= 0) return self.t('mediaLeavingIn') + ' Today';
                    return self.t('mediaLeavingIn') + ' ' + diffDays + ' ' + self.t('mediaDays');
                };

                // Find the play button specifically
                const playButton = document.querySelector('.mainDetailButtons .btnPlay, .detailButtons .btnPlay, button[data-action="resume"], button[data-action="play"]');
                if (playButton && playButton.parentNode) {
                    const badge = document.createElement('span');
                    badge.className = 'detail-leaving-badge';
                    badge.textContent = formatDaysUntil(deletion.DeleteAt);
                    // Insert before the play button
                    playButton.parentNode.insertBefore(badge, playButton);
                }
            }
        },

        /**
         * Update dynamic responsive scaling based on window width
         */
        updateResponsiveScaling: function () {
            const updateScale = () => {
                const width = window.innerWidth;
                let scale = 1;
                let searchWidth = 200; // Default width
                let btnPaddingH = 16; // Horizontal padding for button

                if (width <= 300) {
                    scale = 0.5;
                    searchWidth = 100;
                    btnPaddingH = 8;
                } else if (width <= 500) {
                    // Extra scaling for search field below 500px
                    scale = 0.5 + ((width - 300) / (500 - 300)) * 0.3; // 0.5 to 0.8
                    searchWidth = 100 + ((width - 300) / (500 - 300)) * 50; // 100px to 150px
                    btnPaddingH = 8 + ((width - 300) / (500 - 300)) * 4; // 8px to 12px
                } else if (width < 925) {
                    // Linear interpolation: scale from 1.0 at 925px to 0.8 at 500px
                    scale = 0.8 + ((width - 500) / (925 - 500)) * 0.2;
                    searchWidth = 150 + ((width - 500) / (925 - 500)) * 50; // 150px to 200px
                    btnPaddingH = 12 + ((width - 500) / (925 - 500)) * 4; // 12px to 16px
                }

                // Detect if on Movies or TV Shows page by URL
                const currentUrl = window.location.href;
                const isMoviesOrTVPage = (currentUrl.includes('#/movies?') || currentUrl.includes('#/tv?')) &&
                                        (currentUrl.includes('collectionType=movies') || currentUrl.includes('collectionType=tvshows'));
                const topPosition = (width <= 925 && isMoviesOrTVPage) ? '105px' : (width <= 925 ? '55px' : '');

                // Extend header height when on Movies/TV pages at ≤925px
                const tabsSlider = document.querySelector('.emby-tabs-slider');
                if (tabsSlider) {
                    if (width <= 925 && isMoviesOrTVPage) {
                        tabsSlider.style.paddingBottom = '50px';
                    } else {
                        tabsSlider.style.paddingBottom = '';
                    }
                }

                // Push content down on Movies/TV pages - try multiple containers
                const contentSelectors = [
                    '.mainAnimatedPage',
                    '.page',
                    '[data-role="page"]',
                    '.itemsContainer',
                    '.verticalSection',
                    '.netflix-view-container',
                    '.netflix-genre-row',
                    '.netflix-genre-title'
                ];

                contentSelectors.forEach(selector => {
                    const elements = document.querySelectorAll(selector);
                    elements.forEach(element => {
                        if (element) {
                            if (width <= 925 && isMoviesOrTVPage) {
                                element.style.paddingTop = '50px';
                            } else {
                                element.style.paddingTop = '';
                            }
                        }
                    });
                });

                const searchField = document.getElementById('headerSearchField');
                const searchInput = document.getElementById('headerSearchInput');
                const requestBtn = document.getElementById('requestMediaBtn');

                if (searchField) {
                    if (width <= 925) {
                        searchField.style.transform = `scale(${scale})`;
                        searchField.style.transformOrigin = 'left center';
                        searchField.style.top = topPosition;
                    } else {
                        searchField.style.transform = '';
                        searchField.style.top = '';
                    }
                }

                if (searchInput) {
                    if (width <= 925) {
                        searchInput.style.width = `${searchWidth}px`;
                    } else {
                        searchInput.style.width = '';
                    }
                }

                if (requestBtn) {
                    if (width <= 925) {
                        requestBtn.style.transform = `scale(${scale})`;
                        requestBtn.style.transformOrigin = 'right center';
                        requestBtn.style.paddingLeft = `${btnPaddingH}px`;
                        requestBtn.style.paddingRight = `${btnPaddingH}px`;
                        requestBtn.style.top = topPosition;
                    } else {
                        requestBtn.style.transform = '';
                        requestBtn.style.paddingLeft = '';
                        requestBtn.style.paddingRight = '';
                        requestBtn.style.top = '';
                    }
                }
            };

            // Store updateScale as a method that can be called externally
            self.triggerResponsiveUpdate = updateScale;

            // Update on load
            updateScale();

            // Additional delayed updates for mobile to catch async-created elements
            // This ensures elements created after init get proper positioning
            setTimeout(updateScale, 500);
            setTimeout(updateScale, 1500);
            setTimeout(updateScale, 3000);

            // Update on resize with debounce
            let resizeTimeout;
            window.addEventListener('resize', () => {
                clearTimeout(resizeTimeout);
                resizeTimeout = setTimeout(updateScale, 100);
            });

            // Monitor URL changes for SPA navigation
            let lastUrl = window.location.href;
            const urlCheckInterval = setInterval(() => {
                const currentUrl = window.location.href;
                if (currentUrl !== lastUrl) {
                    lastUrl = currentUrl;
                    // URL changed, update positioning
                    setTimeout(updateScale, 100);
                }
            }, 300);

            // Also listen for popstate (back/forward navigation)
            window.addEventListener('popstate', () => {
                setTimeout(updateScale, 100);
            });

            // Listen for hash changes
            window.addEventListener('hashchange', () => {
                setTimeout(updateScale, 100);
            });

            // MutationObserver to detect when Netflix content is loaded dynamically
            const observer = new MutationObserver((mutations) => {
                for (const mutation of mutations) {
                    // Check if any added nodes contain Netflix genre rows or titles
                    for (const node of mutation.addedNodes) {
                        if (node.nodeType === 1) { // Element node
                            if (node.classList && (node.classList.contains('netflix-genre-row') ||
                                                   node.classList.contains('netflix-view-container') ||
                                                   node.classList.contains('netflix-genre-title'))) {
                                // Netflix content added, trigger update
                                setTimeout(updateScale, 50);
                                return;
                            }
                            // Check children too
                            if (node.querySelector && (node.querySelector('.netflix-genre-row') ||
                                                       node.querySelector('.netflix-view-container') ||
                                                       node.querySelector('.netflix-genre-title'))) {
                                setTimeout(updateScale, 50);
                                return;
                            }
                        }
                    }
                }
            });

            // Start observing the main content area for Netflix content
            const observeTarget = document.querySelector('.mainAnimatedPage') || document.body;
            observer.observe(observeTarget, {
                childList: true,
                subtree: true
            });
        },

        /**
         * Search full library using Jellyfin API and display results
         */
        searchFullLibrary: async function (query) {
            try {
                if (!query || !window.ApiClient) {
                    return;
                }


                const userId = ApiClient.getCurrentUserId();
                const baseUrl = ApiClient.serverAddress();

                // Use Jellyfin's search hints API
                const searchUrl = `${baseUrl}/Search/Hints?SearchTerm=${encodeURIComponent(query)}&UserId=${userId}&IncludeItemTypes=Movie,Series,Episode&Limit=50`;

                const response = await fetch(searchUrl, {
                    headers: {
                        'X-Emby-Authorization': `MediaBrowser Client="Jellyfin Web", Device="Firefox", DeviceId="${ApiClient.deviceId()}", Version="10.11.0", Token="${ApiClient.accessToken()}"`
                    }
                });

                if (!response.ok) {
                    console.error('RatingsPlugin: Search API failed:', response.status);
                    return;
                }

                const data = await response.json();
                const searchItems = data.SearchHints || [];

                // Always remove old results container first
                const oldContainer = document.getElementById('fullLibrarySearchResults');
                if (oldContainer) {
                    oldContainer.remove();
                }

                // Create fresh results container - insert directly into body to avoid Jellyfin page transition issues
                const resultsContainer = document.createElement('div');
                resultsContainer.id = 'fullLibrarySearchResults';
                resultsContainer.style.cssText = `
                    position: fixed;
                    top: 60px;
                    left: 0;
                    right: 0;
                    bottom: 0;
                    padding: 20px;
                    overflow-y: auto;
                    display: block !important;
                    visibility: visible !important;
                    opacity: 1 !important;
                    z-index: 9999;
                    background-color: #0b0b0b;
                `;

                // Insert directly into body to avoid being affected by page transitions
                document.body.appendChild(resultsContainer);

                // Add MutationObserver to detect if something tries to modify the container
                const observer = new MutationObserver((mutations) => {
                    mutations.forEach((mutation) => {
                        if (mutation.type === 'attributes' && mutation.attributeName === 'style') {
                        }
                    });
                });
                observer.observe(resultsContainer, { attributes: true, attributeFilter: ['style'] });

                // Hide original homepage content
                const homeSections = document.querySelectorAll('.verticalSection, .section, .homePageSection');
                homeSections.forEach(section => {
                    section.style.display = 'none';
                });

                // Build results HTML
                let html = `
                    <h2 style="color: #fff; margin-bottom: 20px;">Search Results for "${query}" (${searchItems.length} found)</h2>
                    <div class="itemsContainer vertical-wrap" style="display: flex; flex-wrap: wrap; gap: 20px;">
                `;

                searchItems.forEach(item => {
                    const itemId = item.Id;
                    const itemName = item.Name || 'Unknown';
                    const itemType = item.Type;

                    // Build image URL - use Primary image type
                    const imageSrc = `${baseUrl}/Items/${itemId}/Images/Primary?quality=90&maxWidth=400`;

                    html += `
                        <a href="#!/details?id=${itemId}" class="card portraitCard" style="width: 200px;">
                            <div class="cardBox visualCardBox">
                                <div class="cardScalable">
                                    <div class="cardPadder-portrait"></div>
                                    <div class="cardContent">
                                        <div class="cardImageContainer coveredImage">
                                            <div class="cardPadder-portrait"></div>
                                            <div class="cardImageContainerInner">
                                                <img src="${imageSrc}" class="cardImage itemAction" alt="${itemName}" loading="lazy"/>
                                            </div>
                                        </div>
                                    </div>
                                </div>
                                <div class="cardFooter">
                                    <div class="cardText cardText-first">${itemName}</div>
                                    <div class="cardText cardText-secondary">${itemType}</div>
                                </div>
                            </div>
                        </a>
                    `;
                });

                html += '</div>';
                resultsContainer.innerHTML = html;

                // Check visibility after a delay to detect if Jellyfin page rendering affects it
                setTimeout(() => {
                }, 100);

            } catch (error) {
                console.error('RatingsPlugin: Full library search error:', error);
            }
        },

        /**
         * Clear full library search results and restore homepage
         */
        clearFullLibrarySearch: function () {
            const resultsContainer = document.getElementById('fullLibrarySearchResults');
            if (resultsContainer) {
                resultsContainer.remove();
            }

            // Show original homepage content
            const homeSections = document.querySelectorAll('.verticalSection, .section, .homePageSection');
            homeSections.forEach(section => {
                section.style.display = '';
            });
        },

        /**
         * Hide search dropdown
         */
        hideSearchDropdown: function () {
            const dropdown = document.getElementById('searchDropdown');
            if (dropdown) {
                dropdown.classList.remove('visible');
                dropdown.innerHTML = '';
            }
        },

        /**
         * Position the search dropdown below the search field
         */
        positionSearchDropdown: function () {
            const searchField = document.getElementById('headerSearchField');
            const dropdown = document.getElementById('searchDropdown');
            if (!searchField || !dropdown) return;

            const rect = searchField.getBoundingClientRect();
            dropdown.style.top = (rect.bottom + 4) + 'px';
            dropdown.style.left = rect.left + 'px';
        },

        /**
         * Search library and show results in dropdown
         */
        searchLibraryDropdown: async function (query) {
            const self = this;
            const dropdown = document.getElementById('searchDropdown');

            try {
                if (!query || !window.ApiClient || !dropdown) {
                    return;
                }

                // Position dropdown below search field
                self.positionSearchDropdown();

                const userId = ApiClient.getCurrentUserId();
                const baseUrl = ApiClient.serverAddress();

                // Use Jellyfin's search hints API - search entire library
                const searchUrl = `${baseUrl}/Search/Hints?SearchTerm=${encodeURIComponent(query)}&UserId=${userId}&IncludeItemTypes=Movie,Series&Limit=20`;

                const response = await fetch(searchUrl, {
                    headers: {
                        'X-Emby-Authorization': `MediaBrowser Client="Jellyfin Web", Device="Browser", DeviceId="${ApiClient.deviceId()}", Version="10.11.0", Token="${ApiClient.accessToken()}"`
                    }
                });

                if (!response.ok) {
                    dropdown.innerHTML = '<div class="dropdown-empty">Search failed</div>';
                    return;
                }

                const data = await response.json();
                const searchItems = data.SearchHints || [];

                if (searchItems.length === 0) {
                    dropdown.innerHTML = '<div class="dropdown-empty">No results found</div>';
                    dropdown.classList.add('visible');
                    return;
                }

                // Build dropdown items
                let html = '';
                searchItems.forEach(item => {
                    const itemId = item.Id;
                    const itemName = item.Name || 'Unknown';
                    const itemType = item.Type || '';
                    const itemYear = item.ProductionYear || '';

                    // Build image URL
                    const imageSrc = `${baseUrl}/Items/${itemId}/Images/Primary?quality=90&maxWidth=100`;

                    html += `
                        <a href="#!/details?id=${itemId}" class="dropdown-item" data-item-id="${itemId}">
                            <img src="${imageSrc}" class="dropdown-item-image" alt="" onerror="this.style.display='none'"/>
                            <div class="dropdown-item-info">
                                <div class="dropdown-item-title">${self.escapeHtml(itemName)}</div>
                                <div class="dropdown-item-meta">
                                    <span class="dropdown-item-type">${itemType}</span>
                                    ${itemYear ? `<span class="dropdown-item-year">${itemYear}</span>` : ''}
                                </div>
                            </div>
                        </a>
                    `;
                });

                dropdown.innerHTML = html;
                dropdown.classList.add('visible');

                // Add click handlers to close dropdown after selection
                dropdown.querySelectorAll('.dropdown-item').forEach(item => {
                    item.addEventListener('click', () => {
                        // Clear search and close dropdown
                        const searchInput = document.getElementById('headerSearchInput');
                        const searchIcon = document.getElementById('headerSearchIcon');
                        if (searchInput) {
                            searchInput.value = '';
                        }
                        if (searchIcon) {
                            searchIcon.innerHTML = '🔍';
                            searchIcon.style.fontSize = '18px';
                        }
                        self.hideSearchDropdown();
                    });
                });

            } catch (error) {
                console.error('RatingsPlugin: Dropdown search error:', error);
                dropdown.innerHTML = '<div class="dropdown-empty">Search error</div>';
            }
        },

        /**
         * Filter current page content based on search query
         */
        filterCurrentPageContent: function (query) {
            try {
                const lowerQuery = query.toLowerCase();

                // Find all media cards on the page - comprehensive selector
                const cards = document.querySelectorAll([
                    '.card',
                    '.itemTile',
                    '.portraitCard',
                    '.squareCard',
                    '.overflowPortraitCard',
                    '.overflowSquareCard',
                    '.overflowBackdropCard',
                    '[data-type="Program"]',
                    '[data-type="Movie"]',
                    '[data-type="Series"]',
                    '[data-type="Episode"]',
                    '.listItem',
                    '.netflix-card'  // Netflix view cards
                ].join(', '));

                let matchCount = 0;
                let hideCount = 0;

                cards.forEach(card => {
                    try {
                        // Get card title from various possible locations
                        let title = '';

                        // Try to find title in card text
                        const cardText = card.querySelector('.cardText, .cardTextCentered, .cardText-first, .itemName, .listItemBodyText, .netflix-card-title');
                        if (cardText) {
                            title = cardText.textContent || cardText.innerText || '';
                        }

                        // Also check data attributes
                        if (!title) {
                            title = card.getAttribute('data-title') ||
                                   card.getAttribute('data-name') ||
                                   card.getAttribute('aria-label') ||
                                   card.getAttribute('data-playername') || '';
                        }

                        // Check if link has title
                        if (!title) {
                            const link = card.querySelector('a');
                            if (link) {
                                title = link.getAttribute('title') ||
                                       link.getAttribute('aria-label') ||
                                       link.textContent || '';
                            }
                        }

                        // Check image alt text
                        if (!title) {
                            const img = card.querySelector('img');
                            if (img) {
                                title = img.getAttribute('alt') || '';
                            }
                        }

                        // Filter based on title
                        if (lowerQuery === '' || title.toLowerCase().includes(lowerQuery)) {
                            // Show card
                            card.style.display = '';
                            card.style.opacity = '1';
                            card.style.visibility = 'visible';
                            matchCount++;

                            // Also show parent containers
                            let parent = card.parentElement;
                            while (parent && parent !== document.body) {
                                if (parent.classList.contains('itemsContainer') ||
                                    parent.classList.contains('scrollSlider') ||
                                    parent.classList.contains('itemsWrapper')) {
                                    parent.style.display = '';
                                }
                                parent = parent.parentElement;
                            }
                        } else {
                            // Hide card
                            card.style.display = 'none';
                            card.style.opacity = '0';
                            card.style.visibility = 'hidden';
                            hideCount++;
                        }
                    } catch (err) {
                        // Skip this card if error
                    }
                });

                // Handle sections/rows - hide empty ones
                const sections = document.querySelectorAll('.verticalSection, .section, .homePageSection, .padded-top, .padded-bottom, .netflix-genre-row');
                sections.forEach(section => {
                    try {
                        const visibleCards = section.querySelectorAll('.card:not([style*="display: none"]):not([style*="display:none"]), .itemTile:not([style*="display: none"]):not([style*="display:none"]), .netflix-card:not([style*="display: none"]):not([style*="display:none"])');
                        if (lowerQuery !== '' && visibleCards.length === 0) {
                            section.style.display = 'none';
                        } else {
                            section.style.display = '';
                        }
                    } catch (err) {
                        // Skip this section if error
                    }
                });

            } catch (err) {
                // Silently fail
            }
        },

        /**
         * Load appropriate interface based on user role
         */
        loadRequestInterface: function () {
            const self = this;
            try {
                // Fetch config first to check EnableAdminRequests
                const baseUrl = ApiClient.serverAddress();
                fetch(`${baseUrl}/Ratings/Config`, { method: 'GET', credentials: 'include' })
                    .then(response => response.json())
                    .then(config => {
                        // Check if user is admin
                        self.checkIfAdmin().then(isAdmin => {
                            if (isAdmin) {
                                if (config.EnableAdminRequests) {
                                    // Admin can create requests - show tabs
                                    self.loadAdminWithTabs(config);
                                } else {
                                    // Admin cannot create requests - show manage only
                                    self.loadAdminInterface();
                                }
                            } else {
                                self.loadUserInterface();
                            }
                        }).catch(err => {
                            console.error('Error checking admin status:', err);
                            self.loadUserInterface();
                        });
                    })
                    .catch(err => {
                        console.error('Error fetching config:', err);
                        // Fallback: check admin and show appropriate interface
                        self.checkIfAdmin().then(isAdmin => {
                            if (isAdmin) {
                                self.loadAdminInterface();
                            } else {
                                self.loadUserInterface();
                            }
                        }).catch(() => {
                            self.loadUserInterface();
                        });
                    });
            } catch (err) {
                console.error('Error loading request interface:', err);
            }
        },

        /**
         * Load admin interface with tabs (Create Request / Manage Requests)
         */
        loadAdminWithTabs: function (config) {
            const self = this;
            const modalBody = document.getElementById('requestMediaModalBody');
            const modalTitle = document.getElementById('requestMediaModalTitle');

            if (!modalBody || !modalTitle) return;

            modalTitle.textContent = this.t('requestMedia') || 'Request Media';

            // Create tabs - Manage tab is active by default for admins
            const tabsHtml = `
                <div class="admin-tabs">
                    <button class="admin-tab" data-tab="create">${this.t('createRequest') || 'Create Request'}</button>
                    <button class="admin-tab active" data-tab="manage">${this.t('manageRequests') || 'Manage Requests'}<span class="admin-tab-badge" id="manageTabBadge" style="display:none !important;"></span></button>
                    <button class="admin-tab" data-tab="deletions">${this.t('deletionRequests') || 'Deletion Requests'}<span class="admin-tab-badge" id="deletionsTabBadge" style="display:none !important;"></span></button>
                </div>
                <div class="admin-tab-content" id="adminTabContent"></div>
            `;

            modalBody.innerHTML = tabsHtml;

            // Update tab badges with pending counts
            this.updateAdminTabBadges();

            // Attach tab handlers
            const tabs = modalBody.querySelectorAll('.admin-tab');
            tabs.forEach(tab => {
                tab.addEventListener('click', (e) => {
                    // Remove active from all tabs
                    tabs.forEach(t => t.classList.remove('active'));
                    // Add active to clicked tab
                    e.target.closest('.admin-tab').classList.add('active');
                    // Load appropriate content
                    const tabName = e.target.closest('.admin-tab').getAttribute('data-tab');
                    if (tabName === 'create') {
                        self.renderUserInterfaceInTab(config);
                    } else if (tabName === 'deletions') {
                        self.renderDeletionRequestsTab(config);
                    } else {
                        self.renderAdminInterfaceInTab(config);
                    }
                });
            });

            // Load manage tab by default for admins
            this.renderAdminInterfaceInTab(config);
        },

        /**
         * Render user interface inside tab content
         */
        renderUserInterfaceInTab: function (config) {
            const self = this;
            const tabContent = document.getElementById('adminTabContent');
            if (!tabContent) return;

            // Get custom texts or use defaults
            const windowDesc = config.RequestWindowDescription;
            const titleLabel = config.RequestTitleLabel || this.t('mediaTitle');
            const titlePlaceholder = config.RequestTitlePlaceholder || this.t('mediaTitlePlaceholder');
            const submitText = config.RequestSubmitButtonText || this.t('submitRequest');
            const showLangSwitch = config.ShowLanguageSwitch !== false;

            // Field visibility and required settings
            const typeEnabled = config.RequestTypeEnabled !== false;
            const typeRequired = config.RequestTypeRequired === true;
            const typeLabel = config.RequestTypeLabel || this.t('type');

            const notesEnabled = config.RequestNotesEnabled !== false;
            const notesRequired = config.RequestNotesRequired === true;
            const notesLabel = config.RequestNotesLabel || this.t('additionalNotes');
            const notesPlaceholder = config.RequestNotesPlaceholder || this.t('notesPlaceholder');

            const imdbCodeEnabled = config.RequestImdbCodeEnabled !== false;
            const imdbCodeRequired = config.RequestImdbCodeRequired === true;
            const imdbCodeLabel = config.RequestImdbCodeLabel || 'IMDB Code';
            const imdbCodePlaceholder = config.RequestImdbCodePlaceholder || 'tt0448134';

            const imdbLinkEnabled = config.RequestImdbLinkEnabled !== false;
            const imdbLinkRequired = config.RequestImdbLinkRequired === true;
            const imdbLinkLabel = config.RequestImdbLinkLabel || 'IMDB Link';
            const imdbLinkPlaceholder = config.RequestImdbLinkPlaceholder || 'https://www.imdb.com/title/tt0448134/';

            // Parse custom fields
            let customFields = [];
            if (config.CustomRequestFields) {
                try {
                    customFields = JSON.parse(config.CustomRequestFields);
                } catch (e) {
                    console.error('Error parsing custom fields:', e);
                }
            }

            // Build custom fields HTML
            let customFieldsHtml = '';
            customFields.forEach((field, index) => {
                const fieldId = `customField_${index}`;
                const requiredAttr = field.required ? 'required' : '';
                const requiredMark = field.required ? ' *' : '';
                customFieldsHtml += `
                    <div class="request-input-group">
                        <label for="${fieldId}">${self.escapeHtml(field.name)}${requiredMark}</label>
                        <input type="text" id="${fieldId}" data-field-name="${self.escapeHtml(field.name)}" placeholder="${self.escapeHtml(field.placeholder || '')}" ${requiredAttr} />
                    </div>
                `;
            });

            // Language switch HTML (only if enabled)
            const langSwitchHtml = showLangSwitch ? `
                <div class="language-toggle-container" style="text-align:right;margin-bottom:10px;">
                    ${this.getLanguageSelectorHtml('languageToggle')}
                </div>
            ` : '';

            // Build description HTML (only if configured)
            const descriptionHtml = windowDesc ? `
                <div class="request-description">
                    <strong>${this.t('requestDescription')}</strong><br>
                    ${windowDesc}
                </div>
            ` : '';

            // Build Type field HTML (if enabled)
            const typeHtml = typeEnabled ? `
                <div class="request-input-group">
                    <label for="requestMediaType">${typeLabel}${typeRequired ? ' *' : ''}</label>
                    <select id="requestMediaType" ${typeRequired ? 'required' : ''}>
                        <option value="">${this.t('selectType')}</option>
                        <option value="Movie">${this.t('movie')}</option>
                        <option value="TV Series">${this.t('tvSeries')}</option>
                        <option value="Anime">${this.t('anime')}</option>
                        <option value="Documentary">${this.t('documentary')}</option>
                        <option value="Other">${this.t('other')}</option>
                    </select>
                </div>
            ` : '';

            // Build IMDB Code field HTML (if enabled)
            const imdbCodeHtml = imdbCodeEnabled ? `
                <div class="request-input-group">
                    <label for="requestImdbCode">${imdbCodeLabel}${imdbCodeRequired ? ' *' : ''}</label>
                    <input type="text" id="requestImdbCode" placeholder="${imdbCodePlaceholder}" ${imdbCodeRequired ? 'required' : ''} />
                </div>
            ` : '';

            // Build IMDB Link field HTML (if enabled)
            const imdbLinkHtml = imdbLinkEnabled ? `
                <div class="request-input-group">
                    <label for="requestImdbLink">${imdbLinkLabel}${imdbLinkRequired ? ' *' : ''}</label>
                    <input type="text" id="requestImdbLink" placeholder="${imdbLinkPlaceholder}" ${imdbLinkRequired ? 'required' : ''} />
                </div>
            ` : '';

            // Build Notes field HTML (if enabled)
            const notesHtml = notesEnabled ? `
                <div class="request-input-group">
                    <label for="requestMediaNotes">${notesLabel}${notesRequired ? ' *' : ''}</label>
                    <textarea id="requestMediaNotes" placeholder="${notesPlaceholder}" ${notesRequired ? 'required' : ''}></textarea>
                </div>
            ` : '';

            tabContent.innerHTML = `
                ${langSwitchHtml}
                ${descriptionHtml}
                <div class="request-input-group">
                    <label for="requestMediaTitle">${titleLabel} *</label>
                    <input type="text" id="requestMediaTitle" placeholder="${titlePlaceholder}" required />
                </div>
                ${typeHtml}
                ${imdbCodeHtml}
                ${imdbLinkHtml}
                ${customFieldsHtml}
                ${notesHtml}
                <button class="request-submit-btn" id="submitRequestBtn">${submitText}</button>
                <div class="user-requests-title">${this.t('yourRequests') || 'Your Requests'}</div>
                <div id="userRequestsList"><p style="text-align: center; color: #999;">${this.t('loadingRequests') || 'Loading...'}</p></div>
            `;

            // Attach language selector handler (only if it exists)
            const langToggle = document.getElementById('languageToggle');
            if (langToggle) {
                langToggle.addEventListener('change', () => {
                    self.setLanguage(langToggle.value);
                    self.renderUserInterfaceInTab(config);
                });
            }

            // Attach submit handler
            const submitBtn = document.getElementById('submitRequestBtn');
            if (submitBtn) {
                submitBtn.addEventListener('click', () => {
                    this.submitMediaRequest();
                });
            }

            // Load user's own requests
            this.loadUserRequests();
        },

        /**
         * Render admin interface inside tab content
         */
        renderAdminInterfaceInTab: function (config) {
            const self = this;
            const tabContent = document.getElementById('adminTabContent');
            if (!tabContent) return;

            tabContent.innerHTML = '<p style="text-align: center; color: #999;">' + this.t('loading') + '</p>';

            // Reuse renderAdminInterface logic but target tabContent
            const showLangSwitch = config.ShowLanguageSwitch !== false;

            this.fetchAllRequests().then(requests => {
                const langSwitchHtml = showLangSwitch ? `
                    <div class="language-toggle-container" style="text-align:right;margin-bottom:10px;">
                        ${self.getLanguageSelectorHtml('languageToggleAdmin')}
                    </div>
                ` : '';

                // Build header with language switch only
                let html = langSwitchHtml;

                if (requests.length === 0) {
                    tabContent.innerHTML = html + '<div class="admin-request-empty">' + self.t('noRequestsYet') + '</div>';
                    const langToggle = document.getElementById('languageToggleAdmin');
                    if (langToggle) {
                        langToggle.addEventListener('change', () => {
                            self.setLanguage(langToggle.value);
                            self.renderAdminInterfaceInTab(config);
                        });
                    }
                    return;
                }

                // Group requests by status: new (pending) > processing > snoozed > done > rejected
                const statusOrder = ['new', 'processing', 'snoozed', 'done', 'rejected'];
                const categoryLabels = {
                    new: self.t('categoryNew') || 'New',
                    processing: self.t('categoryProcessing'),
                    snoozed: self.t('categorySnoozed'),
                    done: self.t('categoryDone'),
                    rejected: self.t('categoryRejected')
                };

                // Categorize requests
                const categorized = {
                    new: [],
                    processing: [],
                    snoozed: [],
                    done: [],
                    rejected: []
                };

                requests.forEach(request => {
                    // Check if request is snoozed (has SnoozedUntil date in the future)
                    const isSnoozed = request.SnoozedUntil && new Date(request.SnoozedUntil) > new Date();

                    if (isSnoozed) {
                        categorized.snoozed.push(request);
                    } else if (request.Status === 'pending') {
                        // Pending = New (not yet viewed by admin)
                        categorized.new.push(request);
                    } else if (request.Status === 'processing') {
                        categorized.processing.push(request);
                    } else if (request.Status === 'done') {
                        categorized.done.push(request);
                    } else if (request.Status === 'rejected') {
                        categorized.rejected.push(request);
                    } else {
                        categorized.new.push(request); // Fallback
                    }
                });

                // Category icons for each status
                const categoryIcons = {
                    new: '🆕',
                    processing: '⚙️',
                    snoozed: '💤',
                    done: '✅',
                    rejected: '❌'
                };

                // Build HTML for each category (show all, even empty ones)
                statusOrder.forEach(status => {
                    const categoryRequests = categorized[status];

                    const icon = categoryIcons[status] || '📋';
                    html += `<div class="admin-category-section" data-category="${status}">`;
                    html += `<div class="admin-category-header">
                        <div class="admin-category-header-left">
                            <span class="admin-category-icon">${icon}</span>
                            <span>${categoryLabels[status]}</span>
                        </div>
                        <span class="admin-category-count">${categoryRequests.length}</span>
                        <span class="admin-category-chevron">▼</span>
                    </div>`;
                    html += `<div class="admin-category-content"><ul class="admin-category-list">`;

                    categoryRequests.forEach(request => {
                        html += self.renderAdminRequestItem(request, status === 'snoozed');
                    });

                    html += '</ul></div></div>';
                });

                tabContent.innerHTML = html;

                // Attach language selector handler
                const langToggle = document.getElementById('languageToggleAdmin');
                if (langToggle) {
                    langToggle.addEventListener('change', () => {
                        self.setLanguage(langToggle.value);
                        self.renderAdminInterfaceInTab(config);
                    });
                }

                // Attach category header click handlers (expand/collapse)
                const categoryHeaders = tabContent.querySelectorAll('.admin-category-header');
                categoryHeaders.forEach(header => {
                    header.addEventListener('click', (e) => {
                        const section = header.closest('.admin-category-section');
                        if (section) {
                            section.classList.toggle('expanded');
                        }
                    });
                });

                // Attach request card click handlers (expand/collapse)
                const requestCompacts = tabContent.querySelectorAll('.admin-request-compact');
                requestCompacts.forEach(compact => {
                    compact.addEventListener('click', (e) => {
                        // Don't toggle if clicking on a button or link
                        if (e.target.closest('button') || e.target.closest('a')) return;

                        const item = compact.closest('.admin-request-item');
                        if (item) {
                            // Close other expanded items in the same category
                            const category = item.closest('.admin-category-section');
                            if (category) {
                                category.querySelectorAll('.admin-request-item.expanded').forEach(other => {
                                    if (other !== item) {
                                        other.classList.remove('expanded');
                                    }
                                });
                            }
                            item.classList.toggle('expanded');

                            // Auto-change pending requests to processing when expanded
                            if (item.classList.contains('expanded')) {
                                const requestId = item.getAttribute('data-request-id');
                                const category = item.closest('.admin-category-section');
                                if (requestId && category && category.getAttribute('data-category') === 'new') {
                                    self.autoProcessRequest(requestId);
                                }
                            }
                        }
                    });
                });

                // Prevent clicks inside the expanded panel from collapsing
                const detailsPanels = tabContent.querySelectorAll('.admin-request-details-panel');
                detailsPanels.forEach(panel => {
                    panel.addEventListener('click', (e) => {
                        e.stopPropagation();
                    });
                });

                // Attach status change handlers for buttons
                const statusBtns = tabContent.querySelectorAll('.admin-status-btn');
                statusBtns.forEach(btn => {
                    btn.addEventListener('click', (e) => {
                        const requestId = e.target.getAttribute('data-request-id');
                        const newStatus = e.target.getAttribute('data-status');
                        const linkInput = tabContent.querySelector(`.admin-link-input[data-request-id="${requestId}"]`);
                        const mediaLink = linkInput ? linkInput.value.trim() : '';
                        const rejectionInput = tabContent.querySelector(`.admin-rejection-input[data-request-id="${requestId}"]`);
                        const rejectionReason = rejectionInput ? rejectionInput.value.trim() : '';
                        self.updateRequestStatusInTab(requestId, newStatus, mediaLink, rejectionReason, config);
                    });
                });

                // Attach status change handlers for dropdown
                const statusSelects = tabContent.querySelectorAll('.admin-status-select');
                statusSelects.forEach(select => {
                    select.addEventListener('change', (e) => {
                        const requestId = e.target.getAttribute('data-request-id');
                        const newStatus = e.target.value;
                        const linkInput = tabContent.querySelector(`.admin-link-input[data-request-id="${requestId}"]`);
                        const mediaLink = linkInput ? linkInput.value.trim() : '';
                        const rejectionInput = tabContent.querySelector(`.admin-rejection-input[data-request-id="${requestId}"]`);
                        const rejectionReason = rejectionInput ? rejectionInput.value.trim() : '';
                        self.updateRequestStatusInTab(requestId, newStatus, mediaLink, rejectionReason, config);
                    });
                });

                // Attach snooze handlers
                const snoozeBtns = tabContent.querySelectorAll('.admin-snooze-btn');
                snoozeBtns.forEach(btn => {
                    btn.addEventListener('click', (e) => {
                        const requestId = e.target.getAttribute('data-request-id');
                        const dateInput = tabContent.querySelector(`.admin-snooze-date[data-request-id="${requestId}"]`);
                        if (dateInput && dateInput.value) {
                            self.snoozeRequest(requestId, dateInput.value, config);
                        } else {
                            if (window.require) {
                                require(['toast'], function(toast) {
                                    toast('Please select a snooze date');
                                });
                            }
                        }
                    });
                });

                // Attach unsnooze handlers
                const unsnoozeBtns = tabContent.querySelectorAll('.admin-unsnooze-btn');
                unsnoozeBtns.forEach(btn => {
                    btn.addEventListener('click', (e) => {
                        const requestId = e.target.getAttribute('data-request-id');
                        self.unsnoozeRequest(requestId, config);
                    });
                });

                // Attach delete handlers
                const deleteBtns = tabContent.querySelectorAll('.admin-delete-btn');
                deleteBtns.forEach(btn => {
                    btn.addEventListener('click', (e) => {
                        const requestId = e.target.getAttribute('data-request-id');
                        if (confirm(self.t('confirmDelete'))) {
                            self.deleteRequestInTab(requestId, config);
                        }
                    });
                });

                // Render ban section for media requests
                self.renderBanSection(tabContent, 'media_request', requests);
            }).catch(err => {
                console.error('Error loading requests:', err);
                tabContent.innerHTML = '<div class="admin-request-empty">' + self.t('errorLoading') + '</div>';
            });
        },

        /**
         * Update request status (for tab view)
         */
        updateRequestStatusInTab: function (requestId, status, mediaLink, rejectionReason, config) {
            const self = this;
            const baseUrl = ApiClient.serverAddress();
            const accessToken = ApiClient.accessToken();
            const deviceId = ApiClient.deviceId();

            let url = `${baseUrl}/Ratings/Requests/${requestId}/Status?status=${status}`;
            if (mediaLink) url += `&mediaLink=${encodeURIComponent(mediaLink)}`;
            if (rejectionReason) url += `&rejectionReason=${encodeURIComponent(rejectionReason)}`;

            const authHeader = `MediaBrowser Client="Jellyfin Web", Device="Browser", DeviceId="${deviceId}", Version="10.11.0", Token="${accessToken}"`;

            fetch(url, {
                method: 'POST',
                credentials: 'include',
                headers: { 'X-Emby-Authorization': authHeader }
            })
            .then(response => {
                if (!response.ok) throw new Error('Failed to update status');
                return response.json();
            })
            .then(() => {
                if (window.require) {
                    require(['toast'], function(toast) {
                        toast(self.t('statusUpdated') || 'Status updated');
                    });
                }
                self.renderAdminInterfaceInTab(config);
                self.updateRequestBadge();
            })
            .catch(err => {
                console.error('Error updating status:', err);
                if (window.require) {
                    require(['toast'], function(toast) {
                        toast('Error updating status');
                    });
                }
            });
        },

        /**
         * Delete request (for tab view)
         */
        deleteRequestInTab: function (requestId, config) {
            const self = this;
            const baseUrl = ApiClient.serverAddress();
            const accessToken = ApiClient.accessToken();
            const deviceId = ApiClient.deviceId();
            const url = `${baseUrl}/Ratings/Requests/${requestId}`;

            const authHeader = `MediaBrowser Client="Jellyfin Web", Device="Browser", DeviceId="${deviceId}", Version="10.11.0", Token="${accessToken}"`;

            fetch(url, {
                method: 'DELETE',
                credentials: 'include',
                headers: { 'X-Emby-Authorization': authHeader }
            })
            .then(response => {
                if (!response.ok) throw new Error('Failed to delete');
                return response.json();
            })
            .then(() => {
                if (window.require) {
                    require(['toast'], function(toast) {
                        toast(self.t('requestDeleted') || 'Request deleted');
                    });
                }
                self.renderAdminInterfaceInTab(config);
                self.updateRequestBadge();
            })
            .catch(err => {
                console.error('Error deleting request:', err);
            });
        },

        /**
         * Render a single admin request item
         */
        renderAdminRequestItem: function (request, isSnoozed) {
            const self = this;
            const createdAt = request.CreatedAt ? self.formatDateTime(request.CreatedAt) : self.t('unknown');
            const completedAt = request.CompletedAt ? self.formatDateTime(request.CompletedAt) : null;
            const hasLink = request.MediaLink && request.Status === 'done';
            const isRejected = request.Status === 'rejected';
            const statusText = isSnoozed ? self.t('snoozed') : self.t(request.Status);
            const statusClass = isSnoozed ? 'snoozed' : request.Status;

            // Build custom fields HTML
            let customFieldsHtml = '';
            if (request.CustomFields) {
                try {
                    const customFields = JSON.parse(request.CustomFields);
                    for (const [key, value] of Object.entries(customFields)) {
                        customFieldsHtml += `<div class="admin-request-detail-item"><strong>${self.escapeHtml(key)}:</strong> ${self.escapeHtml(value)}</div>`;
                    }
                } catch (e) {}
            }

            // IMDB display
            let imdbHtml = '';
            if (request.ImdbCode || request.ImdbLink) {
                imdbHtml = `<div class="admin-request-detail-item imdb">`;
                if (request.ImdbCode) {
                    imdbHtml += `<span>🎬 ${self.escapeHtml(request.ImdbCode)}</span>`;
                }
                if (request.ImdbLink) {
                    imdbHtml += `<a href="${self.escapeHtml(request.ImdbLink)}" target="_blank">IMDB →</a>`;
                }
                imdbHtml += `</div>`;
            }

            // Snooze info display
            let snoozeInfoHtml = '';
            if (isSnoozed && request.SnoozedUntil) {
                const snoozedUntilDate = self.formatDateTime(request.SnoozedUntil);
                snoozeInfoHtml = `<div class="admin-request-detail-item">💤 Until: ${snoozedUntilDate}</div>`;
            }

            // Build snooze controls HTML
            let snoozeHtml = '';
            if (isSnoozed) {
                snoozeHtml = `
                    <div class="admin-snooze-controls">
                        <button class="admin-unsnooze-btn" data-request-id="${request.Id}">⏰ ${self.t('unsnooze')}</button>
                    </div>
                `;
            } else if (request.Status !== 'done' && request.Status !== 'rejected') {
                const tomorrow = new Date();
                tomorrow.setDate(tomorrow.getDate() + 1);
                const minDate = tomorrow.toISOString().split('T')[0];
                snoozeHtml = `
                    <div class="admin-snooze-controls">
                        <input type="date" class="admin-snooze-date" data-request-id="${request.Id}" min="${minDate}">
                        <button class="admin-snooze-btn" data-request-id="${request.Id}">💤 ${self.t('snooze')}</button>
                    </div>
                `;
            }

            // Rejection display
            let rejectionHtml = '';
            if (isRejected && request.RejectionReason) {
                rejectionHtml = `<div class="admin-request-rejection">❌ ${self.escapeHtml(request.RejectionReason)}</div>`;
            }

            // Watch button for completed requests
            let watchHtml = '';
            if (hasLink) {
                watchHtml = `<a href="${self.escapeHtml(request.MediaLink)}" class="admin-watch-btn" target="_blank">▶ ${self.t('watchNow')}</a>`;
            }

            return `
                <li class="admin-request-item" data-request-id="${request.Id}">
                    <!-- Compact View (always visible) -->
                    <div class="admin-request-compact">
                        <span class="admin-request-compact-title" title="${self.escapeHtml(request.Title)}">${self.escapeHtml(request.Title)}</span>
                        <div class="admin-request-compact-meta">
                            <span class="admin-request-compact-user">${self.escapeHtml(request.Username)}</span>
                            ${request.Type ? `<span class="admin-request-compact-type">${self.escapeHtml(request.Type)}</span>` : ''}
                            <span class="admin-request-compact-date">${createdAt}</span>
                            <span class="admin-request-compact-status ${statusClass}">${statusText}</span>
                            <span class="admin-request-expand-icon">▼</span>
                        </div>
                    </div>

                    <!-- Expanded Details Panel (hidden by default) -->
                    <div class="admin-request-details-panel">
                        <div class="admin-request-details-content">
                            <!-- Info row -->
                            <div class="admin-request-detail-row">
                                ${imdbHtml}
                                ${snoozeInfoHtml}
                                ${customFieldsHtml}
                                ${completedAt ? `<div class="admin-request-detail-item">✅ Completed: ${completedAt}</div>` : ''}
                            </div>

                            <!-- Notes -->
                            ${request.Notes ? `<div class="admin-request-notes">${self.escapeHtml(request.Notes)}</div>` : ''}

                            <!-- Rejection reason -->
                            ${rejectionHtml}

                            <!-- Action buttons -->
                            <div class="admin-request-actions-row">
                                <button class="admin-action-btn pending admin-status-btn" data-status="pending" data-request-id="${request.Id}">${self.t('pending')}</button>
                                <button class="admin-action-btn processing admin-status-btn" data-status="processing" data-request-id="${request.Id}">${self.t('processing')}</button>
                                <button class="admin-action-btn done admin-status-btn" data-status="done" data-request-id="${request.Id}">${self.t('done')}</button>
                                <button class="admin-action-btn rejected admin-status-btn" data-status="rejected" data-request-id="${request.Id}">${self.t('rejected')}</button>
                                <button class="admin-action-btn delete admin-delete-btn" data-request-id="${request.Id}">🗑️</button>
                            </div>

                            <!-- Input fields -->
                            <div class="admin-request-inputs">
                                <input type="text" class="admin-request-input admin-link-input" data-request-id="${request.Id}" placeholder="${self.t('mediaLinkPlaceholder')}" value="${self.escapeHtml(request.MediaLink || '')}">
                                <input type="text" class="admin-request-input admin-rejection-input" data-request-id="${request.Id}" placeholder="Rejection reason..." value="${self.escapeHtml(request.RejectionReason || '')}">
                            </div>

                            <!-- Snooze controls -->
                            ${snoozeHtml}

                            <!-- Watch button -->
                            ${watchHtml}
                        </div>
                    </div>

                    <!-- Hidden mobile elements -->
                    <select class="admin-status-select" data-request-id="${request.Id}">
                        <option value="pending" ${request.Status === 'pending' ? 'selected' : ''}>${self.t('pending')}</option>
                        <option value="processing" ${request.Status === 'processing' ? 'selected' : ''}>${self.t('processing')}</option>
                        <option value="done" ${request.Status === 'done' ? 'selected' : ''}>${self.t('done')}</option>
                        <option value="rejected" ${request.Status === 'rejected' ? 'selected' : ''}>${self.t('rejected')}</option>
                    </select>
                </li>
            `;
        },

        /**
         * Snooze a request until a specified date
         */
        snoozeRequest: function (requestId, snoozedUntil, config) {
            const self = this;
            const baseUrl = ApiClient.serverAddress();
            const accessToken = ApiClient.accessToken();
            const deviceId = ApiClient.deviceId();
            const url = `${baseUrl}/Ratings/Requests/${requestId}/Snooze?snoozedUntil=${encodeURIComponent(snoozedUntil)}`;

            const authHeader = `MediaBrowser Client="Jellyfin Web", Device="Browser", DeviceId="${deviceId}", Version="10.11.0", Token="${accessToken}"`;

            fetch(url, {
                method: 'POST',
                credentials: 'include',
                headers: { 'X-Emby-Authorization': authHeader }
            })
            .then(response => {
                if (!response.ok) throw new Error('Failed to snooze');
                return response.json();
            })
            .then(() => {
                if (window.require) {
                    require(['toast'], function(toast) {
                        toast(self.t('statusUpdated') || 'Request snoozed');
                    });
                }
                self.renderAdminInterfaceInTab(config);
            })
            .catch(err => {
                console.error('Error snoozing request:', err);
                if (window.require) {
                    require(['toast'], function(toast) {
                        toast('Error snoozing request');
                    });
                }
            });
        },

        /**
         * Unsnooze a request
         */
        unsnoozeRequest: function (requestId, config) {
            const self = this;
            const baseUrl = ApiClient.serverAddress();
            const accessToken = ApiClient.accessToken();
            const deviceId = ApiClient.deviceId();
            const url = `${baseUrl}/Ratings/Requests/${requestId}/Unsnooze`;

            const authHeader = `MediaBrowser Client="Jellyfin Web", Device="Browser", DeviceId="${deviceId}", Version="10.11.0", Token="${accessToken}"`;

            fetch(url, {
                method: 'POST',
                credentials: 'include',
                headers: { 'X-Emby-Authorization': authHeader }
            })
            .then(response => {
                if (!response.ok) throw new Error('Failed to unsnooze');
                return response.json();
            })
            .then(() => {
                if (window.require) {
                    require(['toast'], function(toast) {
                        toast(self.t('statusUpdated') || 'Request unsnoozed');
                    });
                }
                self.renderAdminInterfaceInTab(config);
            })
            .catch(err => {
                console.error('Error unsnoozing request:', err);
                if (window.require) {
                    require(['toast'], function(toast) {
                        toast('Error unsnoozing request');
                    });
                }
            });
        },

        /**
         * Check if current user is admin
         */
        checkIfAdmin: function () {
            return new Promise((resolve, reject) => {
                try {
                    if (!window.ApiClient) {
                        resolve(false);
                        return;
                    }

                    const userId = ApiClient.getCurrentUserId();
                    const baseUrl = ApiClient.serverAddress();
                    const accessToken = ApiClient.accessToken();

                    if (!userId) {
                        resolve(false);
                        return;
                    }

                    const url = `${baseUrl}/Users/${userId}`;
                    const deviceId = ApiClient.deviceId();
                    const authHeader = `MediaBrowser Client="Jellyfin Web", Device="Browser", DeviceId="${deviceId}", Version="10.11.0", Token="${accessToken}"`;

                    fetch(url, {
                        method: 'GET',
                        credentials: 'include',
                        headers: {
                            'Content-Type': 'application/json',
                            'X-Emby-Authorization': authHeader
                        }
                    })
                    .then(response => response.json())
                    .then(user => {
                        resolve(user.Policy && user.Policy.IsAdministrator === true);
                    })
                    .catch(err => {
                        console.error('Error fetching user info:', err);
                        resolve(false);
                    });
                } catch (err) {
                    console.error('Error in checkIfAdmin:', err);
                    resolve(false);
                }
            });
        },

        /**
         * Load user interface for making requests
         */
        loadUserInterface: function () {
            const self = this;
            const modalBody = document.getElementById('requestMediaModalBody');
            const modalTitle = document.getElementById('requestMediaModalTitle');

            if (!modalBody || !modalTitle) return;

            // Clear viewed requests when user opens modal
            this.markDoneRequestsAsViewed();

            // Fetch config and render with custom settings
            const baseUrl = ApiClient.serverAddress();
            fetch(`${baseUrl}/Ratings/Config`, { method: 'GET', credentials: 'include' })
                .then(response => response.json())
                .then(config => {
                    self.renderUserInterface(modalBody, modalTitle, config);
                })
                .catch(() => {
                    // Fallback with default config
                    self.renderUserInterface(modalBody, modalTitle, {});
                });
        },

        /**
         * Render user interface with config
         */
        renderUserInterface: function (modalBody, modalTitle, config) {
            const self = this;

            // Get custom texts or use defaults
            const windowTitle = config.RequestWindowTitle || this.t('requestMedia');
            const windowDesc = config.RequestWindowDescription;
            const titleLabel = config.RequestTitleLabel || this.t('mediaTitle');
            const titlePlaceholder = config.RequestTitlePlaceholder || this.t('mediaTitlePlaceholder');
            const submitText = config.RequestSubmitButtonText || this.t('submitRequest');
            const showLangSwitch = config.ShowLanguageSwitch !== false;

            // Field visibility and required settings
            const typeEnabled = config.RequestTypeEnabled !== false;
            const typeRequired = config.RequestTypeRequired === true;
            const typeLabel = config.RequestTypeLabel || this.t('type');

            const notesEnabled = config.RequestNotesEnabled !== false;
            const notesRequired = config.RequestNotesRequired === true;
            const notesLabel = config.RequestNotesLabel || this.t('additionalNotes');
            const notesPlaceholder = config.RequestNotesPlaceholder || this.t('notesPlaceholder');

            const imdbCodeEnabled = config.RequestImdbCodeEnabled !== false;
            const imdbCodeRequired = config.RequestImdbCodeRequired === true;
            const imdbCodeLabel = config.RequestImdbCodeLabel || 'IMDB Code';
            const imdbCodePlaceholder = config.RequestImdbCodePlaceholder || 'tt0448134';

            const imdbLinkEnabled = config.RequestImdbLinkEnabled !== false;
            const imdbLinkRequired = config.RequestImdbLinkRequired === true;
            const imdbLinkLabel = config.RequestImdbLinkLabel || 'IMDB Link';
            const imdbLinkPlaceholder = config.RequestImdbLinkPlaceholder || 'https://www.imdb.com/title/tt0448134/';

            // Parse custom fields
            let customFields = [];
            if (config.CustomRequestFields) {
                try {
                    customFields = JSON.parse(config.CustomRequestFields);
                } catch (e) {
                    console.error('Error parsing custom fields:', e);
                }
            }

            // Build custom fields HTML
            let customFieldsHtml = '';
            customFields.forEach((field, index) => {
                const fieldId = `customField_${index}`;
                const requiredAttr = field.required ? 'required' : '';
                const requiredMark = field.required ? ' *' : '';
                customFieldsHtml += `
                    <div class="request-input-group">
                        <label for="${fieldId}">${self.escapeHtml(field.name)}${requiredMark}</label>
                        <input type="text" id="${fieldId}" data-field-name="${self.escapeHtml(field.name)}" placeholder="${self.escapeHtml(field.placeholder || '')}" ${requiredAttr} />
                    </div>
                `;
            });

            // Language switch HTML (only if enabled)
            const langSwitchHtml = showLangSwitch ? `
                <div class="language-toggle-container" style="text-align:right;margin-bottom:10px;">
                    ${this.getLanguageSelectorHtml('languageToggle')}
                </div>
            ` : '';

            // Build description HTML (only if configured)
            const descriptionHtml = windowDesc ? `
                <div class="request-description">
                    <strong>${this.t('requestDescription')}</strong><br>
                    ${windowDesc}
                </div>
            ` : '';

            // Build Type field HTML (if enabled)
            const typeHtml = typeEnabled ? `
                <div class="request-input-group">
                    <label for="requestMediaType">${typeLabel}${typeRequired ? ' *' : ''}</label>
                    <select id="requestMediaType" ${typeRequired ? 'required' : ''}>
                        <option value="">${this.t('selectType')}</option>
                        <option value="Movie">${this.t('movie')}</option>
                        <option value="TV Series">${this.t('tvSeries')}</option>
                        <option value="Anime">${this.t('anime')}</option>
                        <option value="Documentary">${this.t('documentary')}</option>
                        <option value="Other">${this.t('other')}</option>
                    </select>
                </div>
            ` : '';

            // Build IMDB Code field HTML (if enabled)
            const imdbCodeHtml = imdbCodeEnabled ? `
                <div class="request-input-group">
                    <label for="requestImdbCode">${imdbCodeLabel}${imdbCodeRequired ? ' *' : ''}</label>
                    <input type="text" id="requestImdbCode" placeholder="${imdbCodePlaceholder}" ${imdbCodeRequired ? 'required' : ''} />
                </div>
            ` : '';

            // Build IMDB Link field HTML (if enabled)
            const imdbLinkHtml = imdbLinkEnabled ? `
                <div class="request-input-group">
                    <label for="requestImdbLink">${imdbLinkLabel}${imdbLinkRequired ? ' *' : ''}</label>
                    <input type="text" id="requestImdbLink" placeholder="${imdbLinkPlaceholder}" ${imdbLinkRequired ? 'required' : ''} />
                </div>
            ` : '';

            // Build Notes field HTML (if enabled)
            const notesHtml = notesEnabled ? `
                <div class="request-input-group">
                    <label for="requestMediaNotes">${notesLabel}${notesRequired ? ' *' : ''}</label>
                    <textarea id="requestMediaNotes" placeholder="${notesPlaceholder}" ${notesRequired ? 'required' : ''}></textarea>
                </div>
            ` : '';

            modalTitle.textContent = windowTitle;
            modalBody.innerHTML = `
                ${langSwitchHtml}
                ${descriptionHtml}
                <div id="mediaRequestBanNotice" style="display:none;"></div>
                <div id="mediaRequestFormFields">
                    <div class="request-input-group">
                        <label for="requestMediaTitle">${titleLabel} *</label>
                        <input type="text" id="requestMediaTitle" placeholder="${titlePlaceholder}" required />
                    </div>
                    ${typeHtml}
                    ${imdbCodeHtml}
                    ${imdbLinkHtml}
                    ${customFieldsHtml}
                    ${notesHtml}
                    <button class="request-submit-btn" id="submitRequestBtn">${submitText}</button>
                </div>
                <div class="user-requests-title">${this.t('yourRequests')}</div>
                <div id="userRequestsList"><p style="text-align: center; color: #999;">${this.t('loadingRequests')}</p></div>
            `;

            // Check if user is banned from media requests
            this.checkBan('media_request').then(banInfo => {
                if (banInfo && banInfo.banned) {
                    const formFields = document.getElementById('mediaRequestFormFields');
                    const banNotice = document.getElementById('mediaRequestBanNotice');
                    if (formFields) formFields.style.display = 'none';
                    if (banNotice) {
                        let expiresText = '';
                        if (banInfo.expiresAt) {
                            expiresText = ` (${self.t('banExpires')} ${self.formatDateTime(banInfo.expiresAt)})`;
                        } else {
                            expiresText = ` (${self.t('banPermanentLabel')})`;
                        }
                        banNotice.innerHTML = `<div class="ban-notice">${self.t('youAreBanned')}${expiresText}</div>`;
                        banNotice.style.display = 'block';
                    }
                }
            });

            // Attach language selector handler (only if it exists)
            const langToggle = document.getElementById('languageToggle');
            if (langToggle) {
                langToggle.addEventListener('change', () => {
                    self.setLanguage(langToggle.value);
                    self.loadUserInterface(); // Reload interface with new language
                });
            }

            // Attach submit handler
            const submitBtn = document.getElementById('submitRequestBtn');
            if (submitBtn) {
                submitBtn.addEventListener('click', () => {
                    this.submitMediaRequest();
                });
            }

            // Load user's own requests
            this.loadUserRequests();
        },

        /**
         * Load user's own requests
         */
        loadUserRequests: function () {
            const self = this;
            const listContainer = document.getElementById('userRequestsList');

            if (!listContainer) return;

            listContainer.innerHTML = '<p style="text-align: center; color: #999;">' + this.t('loadingRequests') + '</p>';

            // Fetch media requests, deletion requests, and ban status
            Promise.all([
                this.fetchAllRequests(),
                this.fetchDeletionRequests(),
                this.checkBan('deletion_request')
            ]).then(([requests, deletionRequests, deletionBanInfo]) => {
                // Filter to only current user's requests
                const userId = ApiClient.getCurrentUserId();
                const userRequests = requests.filter(r => r.UserId === userId);

                if (userRequests.length === 0) {
                    listContainer.innerHTML = '<p style="text-align: center; color: #999;">' + self.t('noRequests') + '</p>';
                    return;
                }

                let html = '<ul class="user-request-list">';
                userRequests.forEach(request => {
                    // Format timestamps
                    const createdAt = request.CreatedAt ? self.formatDateTime(request.CreatedAt) : '';
                    const completedAt = request.CompletedAt ? self.formatDateTime(request.CompletedAt) : null;
                    const hasLink = request.MediaLink && request.Status === 'done';
                    const isRejected = request.Status === 'rejected';
                    const statusText = self.t(request.Status);

                    // Parse custom fields if present
                    let customFieldsHtml = '';
                    if (request.CustomFields) {
                        try {
                            const customFields = JSON.parse(request.CustomFields);
                            for (const [key, value] of Object.entries(customFields)) {
                                customFieldsHtml += `<div class="user-request-custom-field"><strong>${self.escapeHtml(key)}:</strong> ${self.escapeHtml(value)}</div>`;
                            }
                        } catch (e) {
                            // Ignore parse errors
                        }
                    }

                    // Rejection reason
                    const rejectionHtml = isRejected && request.RejectionReason
                        ? `<div class="user-request-rejection-reason">❌ ${self.escapeHtml(request.RejectionReason)}</div>`
                        : '';

                    // IMDB info
                    let imdbHtml = '';
                    if (request.ImdbCode) {
                        imdbHtml += `<div class="user-request-imdb"><strong>IMDB:</strong> ${self.escapeHtml(request.ImdbCode)}</div>`;
                    }
                    if (request.ImdbLink) {
                        imdbHtml += `<div class="user-request-imdb"><a href="${self.escapeHtml(request.ImdbLink)}" target="_blank" class="imdb-link">View on IMDB</a></div>`;
                    }

                    // Edit/Delete buttons only for pending requests
                    const isPending = request.Status === 'pending';
                    const actionsHtml = isPending ? `
                        <div class="user-request-actions">
                            <button class="user-edit-btn" data-request-id="${request.Id}" data-request-title="${self.escapeHtml(request.Title)}" data-request-type="${self.escapeHtml(request.Type || '')}" data-request-notes="${self.escapeHtml(request.Notes || '')}" data-request-imdb-code="${self.escapeHtml(request.ImdbCode || '')}" data-request-imdb-link="${self.escapeHtml(request.ImdbLink || '')}" data-request-custom-fields="${self.escapeHtml(request.CustomFields || '')}">✏️ ${self.t('edit') || 'Edit'}</button>
                            <button class="user-delete-btn" data-request-id="${request.Id}">🗑️ ${self.t('delete') || 'Delete'}</button>
                        </div>
                    ` : '';

                    html += `
                        <li class="user-request-item">
                            <div class="user-request-info">
                                <div class="user-request-item-title">${self.escapeHtml(request.Title)}</div>
                                <div class="user-request-item-type">${request.Type ? self.escapeHtml(request.Type) : self.t('notSpecified')}</div>
                                ${imdbHtml}
                                ${customFieldsHtml}
                                <div class="user-request-time">📅 ${createdAt}${completedAt ? ` • ✅ ${completedAt}` : ''}</div>
                                ${rejectionHtml}
                                ${hasLink ? `<a href="${self.escapeHtml(request.MediaLink)}" class="request-media-link" target="_blank">${self.t('watchNow')}</a>` : ''}
                                ${(() => {
                                    const isDeletionBanned = deletionBanInfo && deletionBanInfo.banned;
                                    const hasPendingDeletion = deletionRequests.some(dr => dr.MediaRequestId === request.Id && dr.Status === 'pending');
                                    if (hasPendingDeletion) {
                                        return `<span class="deletion-requested-text">${self.t('alreadyRequested')}</span>`;
                                    }
                                    // Show all rejected deletion requests with reasons (clickable)
                                    const rejectedDeletions = deletionRequests.filter(dr => dr.MediaRequestId === request.Id && dr.Status === 'rejected');
                                    let rejectedHtml = '';
                                    if (rejectedDeletions.length > 0) {
                                        const sorted = rejectedDeletions.sort((a, b) => new Date(b.ResolvedAt) - new Date(a.ResolvedAt));
                                        const hasAnyReason = sorted.some(r => r.RejectionReason);
                                        const reasonsData = self.escapeHtml(JSON.stringify(sorted.map(r => ({
                                            reason: r.RejectionReason || '',
                                            admin: r.ResolvedByUsername || '',
                                            date: r.ResolvedAt || ''
                                        }))));
                                        rejectedHtml = `<span class="deletion-rejected-text rejection-reason-trigger" data-reasons="${reasonsData}" style="cursor:pointer;">${self.t('deletionRejected')} (${sorted.length})${hasAnyReason ? ' ℹ️' : ''}</span>`;
                                    }
                                    const isDone = request.Status === 'done';
                                    const isRejectedStatus = request.Status === 'rejected';
                                    // Count total deletion requests for this media request (for limit check)
                                    const totalDeletionRequests = deletionRequests.filter(dr => dr.MediaRequestId === request.Id).length;
                                    if (totalDeletionRequests >= 3) {
                                        return rejectedHtml + `<span class="deletion-requested-text">${self.t('deletionLimitReached')}</span>`;
                                    }
                                    if (isDeletionBanned) {
                                        let banExpText = '';
                                        if (deletionBanInfo.expiresAt) {
                                            banExpText = ` (${self.t('banExpires')} ${self.formatDateTime(deletionBanInfo.expiresAt)})`;
                                        }
                                        return rejectedHtml + `<span class="ban-notice" style="font-size:11px;padding:4px 8px;margin-top:4px;display:inline-block;">${self.t('youAreBanned')}${banExpText}</span>`;
                                    }
                                    if (isDone && hasLink) {
                                        // "Request to delete media" for fulfilled requests
                                        const itemId = self.extractItemIdFromLink(request.MediaLink) || '';
                                        return rejectedHtml + `<button class="deletion-request-btn" data-request-id="${request.Id}" data-item-id="${itemId}" data-title="${self.escapeHtml(request.Title)}" data-type="${self.escapeHtml(request.Type || '')}" data-media-link="${self.escapeHtml(request.MediaLink)}" data-deletion-type="media">${self.t('requestDeleteMedia')}</button>`;
                                    } else if (!isDone && !isRejectedStatus) {
                                        // "Request to delete request" for non-done, non-rejected requests
                                        return rejectedHtml + `<button class="deletion-request-btn delete-request-type" data-request-id="${request.Id}" data-item-id="" data-title="${self.escapeHtml(request.Title)}" data-type="${self.escapeHtml(request.Type || '')}" data-media-link="" data-deletion-type="request">${self.t('requestDeleteRequest')}</button>`;
                                    }
                                    return rejectedHtml;
                                })()}
                                ${actionsHtml}
                            </div>
                            <span class="user-request-status ${request.Status}">${statusText}</span>
                        </li>
                    `;
                });
                html += '</ul>';
                listContainer.innerHTML = html;

                // Attach edit button handlers
                const editBtns = listContainer.querySelectorAll('.user-edit-btn');
                editBtns.forEach(btn => {
                    btn.addEventListener('click', (e) => {
                        const requestId = e.target.getAttribute('data-request-id');
                        const title = e.target.getAttribute('data-request-title');
                        const type = e.target.getAttribute('data-request-type');
                        const notes = e.target.getAttribute('data-request-notes');
                        const imdbCode = e.target.getAttribute('data-request-imdb-code');
                        const imdbLink = e.target.getAttribute('data-request-imdb-link');
                        const customFields = e.target.getAttribute('data-request-custom-fields');
                        self.showEditRequestForm(requestId, title, type, notes, imdbCode, imdbLink, customFields);
                    });
                });

                // Attach delete button handlers
                const deleteBtns = listContainer.querySelectorAll('.user-delete-btn');
                deleteBtns.forEach(btn => {
                    btn.addEventListener('click', (e) => {
                        const requestId = e.target.getAttribute('data-request-id');
                        if (confirm(self.t('confirmDelete') || 'Are you sure you want to delete this request?')) {
                            self.deleteUserRequest(requestId);
                        }
                    });
                });

                // Attach "Request to delete" button handlers
                const askDeleteBtns = listContainer.querySelectorAll('.deletion-request-btn');
                askDeleteBtns.forEach(btn => {
                    btn.addEventListener('click', (e) => {
                        const target = e.target;
                        const mediaRequestId = target.getAttribute('data-request-id');
                        const itemId = target.getAttribute('data-item-id');
                        const title = target.getAttribute('data-title');
                        const type = target.getAttribute('data-type');
                        const mediaLink = target.getAttribute('data-media-link');
                        const deletionType = target.getAttribute('data-deletion-type') || 'media';
                        self.submitDeletionRequest(mediaRequestId, itemId, title, type, mediaLink, deletionType, target);
                    });
                });

                // Attach rejection reason popup handlers
                const rejectionTriggers = listContainer.querySelectorAll('.rejection-reason-trigger[data-reasons]');
                rejectionTriggers.forEach(trigger => {
                    trigger.addEventListener('click', () => {
                        try {
                            const reasons = JSON.parse(trigger.getAttribute('data-reasons'));
                            if (reasons && reasons.length > 0) {
                                self.showRejectionReasonsPopup(reasons);
                            }
                        } catch (e) {}
                    });
                });
            }).catch(err => {
                console.error('Error loading user requests:', err);
                listContainer.innerHTML = '<p style="text-align: center; color: #f44336;">' + self.t('errorLoading') + '</p>';
            });
        },

        /**
         * Show edit form for a request
         */
        showEditRequestForm: function (requestId, title, type, notes, imdbCode, imdbLink, customFields) {
            const self = this;

            // Fill the existing form with request data
            const titleInput = document.getElementById('requestMediaTitle');
            const typeSelect = document.getElementById('requestMediaType');
            const notesInput = document.getElementById('requestMediaNotes');
            const imdbCodeInput = document.getElementById('requestImdbCode');
            const imdbLinkInput = document.getElementById('requestImdbLink');

            if (titleInput) titleInput.value = title || '';
            if (typeSelect) typeSelect.value = type || '';
            if (notesInput) notesInput.value = notes || '';
            if (imdbCodeInput) imdbCodeInput.value = imdbCode || '';
            if (imdbLinkInput) imdbLinkInput.value = imdbLink || '';

            // Parse and fill custom fields
            if (customFields) {
                try {
                    const parsedFields = JSON.parse(customFields);
                    for (const [key, value] of Object.entries(parsedFields)) {
                        const customInput = document.querySelector(`[data-field-name="${key}"]`);
                        if (customInput) customInput.value = value || '';
                    }
                } catch (e) {
                    // Ignore parse errors
                }
            }

            // Change submit button to update mode
            const submitBtn = document.getElementById('submitRequestBtn');
            if (submitBtn) {
                submitBtn.textContent = self.t('updateRequest') || 'Update Request';
                submitBtn.setAttribute('data-edit-mode', 'true');
                submitBtn.setAttribute('data-request-id', requestId);

                // Remove old listener and add new one
                const newBtn = submitBtn.cloneNode(true);
                submitBtn.parentNode.replaceChild(newBtn, submitBtn);
                newBtn.addEventListener('click', () => {
                    self.updateUserRequest(requestId);
                });
            }

            // Scroll to form
            if (titleInput) {
                titleInput.focus();
                titleInput.scrollIntoView({ behavior: 'smooth', block: 'center' });
            }
        },

        /**
         * Update user's own request
         */
        updateUserRequest: function (requestId) {
            const self = this;

            const title = document.getElementById('requestMediaTitle').value.trim();
            const typeEl = document.getElementById('requestMediaType');
            const type = typeEl ? typeEl.value.trim() : '';
            const notesEl = document.getElementById('requestMediaNotes');
            const notes = notesEl ? notesEl.value.trim() : '';
            const imdbCodeEl = document.getElementById('requestImdbCode');
            const imdbCode = imdbCodeEl ? imdbCodeEl.value.trim() : '';
            const imdbLinkEl = document.getElementById('requestImdbLink');
            const imdbLink = imdbLinkEl ? imdbLinkEl.value.trim() : '';

            if (!title) {
                if (window.require) {
                    require(['toast'], function(toast) {
                        toast(self.t('titleRequired') || 'Title is required');
                    });
                }
                return;
            }

            // Collect custom fields
            const customFieldInputs = document.querySelectorAll('[id^="customField_"]');
            const customFieldsObj = {};
            customFieldInputs.forEach(input => {
                const fieldName = input.getAttribute('data-field-name');
                const value = input.value.trim();
                if (fieldName && value) {
                    customFieldsObj[fieldName] = value;
                }
            });

            const baseUrl = ApiClient.serverAddress();
            const accessToken = ApiClient.accessToken();
            const deviceId = ApiClient.deviceId();
            const url = `${baseUrl}/Ratings/Requests/${requestId}`;

            const authHeader = `MediaBrowser Client="Jellyfin Web", Device="Browser", DeviceId="${deviceId}", Version="10.11.0", Token="${accessToken}"`;

            const requestData = {
                Title: title,
                Type: type,
                Notes: notes,
                CustomFields: Object.keys(customFieldsObj).length > 0 ? JSON.stringify(customFieldsObj) : '',
                ImdbCode: imdbCode,
                ImdbLink: imdbLink
            };

            fetch(url, {
                method: 'PUT',
                credentials: 'include',
                headers: {
                    'Content-Type': 'application/json',
                    'X-Emby-Authorization': authHeader
                },
                body: JSON.stringify(requestData)
            })
            .then(response => {
                if (!response.ok) {
                    return response.text().then(text => {
                        throw new Error(text || 'Failed to update request');
                    });
                }
                return response.json();
            })
            .then(data => {
                if (window.require) {
                    require(['toast'], function(toast) {
                        toast(self.t('requestUpdated') || 'Request updated successfully');
                    });
                }

                // Reset form to create mode
                self.resetRequestForm();

                // Reload user requests
                self.loadUserRequests();
            })
            .catch(err => {
                console.error('Error updating request:', err);
                if (window.require) {
                    require(['toast'], function(toast) {
                        toast(err.message || 'Error updating request');
                    });
                }
            });
        },

        /**
         * Delete user's own request
         */
        deleteUserRequest: function (requestId) {
            const self = this;

            const baseUrl = ApiClient.serverAddress();
            const accessToken = ApiClient.accessToken();
            const deviceId = ApiClient.deviceId();
            const url = `${baseUrl}/Ratings/Requests/${requestId}`;

            const authHeader = `MediaBrowser Client="Jellyfin Web", Device="Browser", DeviceId="${deviceId}", Version="10.11.0", Token="${accessToken}"`;

            fetch(url, {
                method: 'DELETE',
                credentials: 'include',
                headers: {
                    'Content-Type': 'application/json',
                    'X-Emby-Authorization': authHeader
                }
            })
            .then(response => {
                if (!response.ok) {
                    throw new Error('Failed to delete request');
                }
                return response.json();
            })
            .then(data => {
                if (window.require) {
                    require(['toast'], function(toast) {
                        toast(self.t('requestDeleted') || 'Request deleted successfully');
                    });
                }

                // Reload user requests
                self.loadUserRequests();
            })
            .catch(err => {
                console.error('Error deleting request:', err);
                if (window.require) {
                    require(['toast'], function(toast) {
                        toast('Error deleting request');
                    });
                }
            });
        },

        /**
         * Reset request form to create mode
         */
        resetRequestForm: function () {
            const self = this;

            // Clear form fields
            const titleInput = document.getElementById('requestMediaTitle');
            const typeSelect = document.getElementById('requestMediaType');
            const notesInput = document.getElementById('requestMediaNotes');
            const imdbCodeInput = document.getElementById('requestImdbCode');
            const imdbLinkInput = document.getElementById('requestImdbLink');

            if (titleInput) titleInput.value = '';
            if (typeSelect) typeSelect.value = '';
            if (notesInput) notesInput.value = '';
            if (imdbCodeInput) imdbCodeInput.value = '';
            if (imdbLinkInput) imdbLinkInput.value = '';

            // Clear custom fields
            const customFieldInputs = document.querySelectorAll('[id^="customField_"]');
            customFieldInputs.forEach(input => {
                input.value = '';
            });

            // Reset submit button
            const submitBtn = document.getElementById('submitRequestBtn');
            if (submitBtn) {
                submitBtn.textContent = self.t('submitRequest') || 'Submit Request';
                submitBtn.removeAttribute('data-edit-mode');
                submitBtn.removeAttribute('data-request-id');

                // Remove old listener and add new one for create
                const newBtn = submitBtn.cloneNode(true);
                submitBtn.parentNode.replaceChild(newBtn, submitBtn);
                newBtn.addEventListener('click', () => {
                    self.submitMediaRequest();
                });
            }
        },

        /**
         * Load admin interface for managing requests
         */
        loadAdminInterface: function () {
            const self = this;
            const modalBody = document.getElementById('requestMediaModalBody');
            const modalTitle = document.getElementById('requestMediaModalTitle');

            if (!modalBody || !modalTitle) return;

            modalTitle.textContent = this.t('manageRequests');
            modalBody.innerHTML = '<p style="text-align: center; color: #999;">' + this.t('loading') + '</p>';

            // Fetch config and then render with tabs
            const baseUrl = ApiClient.serverAddress();
            fetch(`${baseUrl}/Ratings/Config`, { method: 'GET', credentials: 'include' })
                .then(response => response.json())
                .then(config => {
                    // Show tabs: Manage Requests + Deletion Requests
                    const tabsHtml = `
                        <div class="admin-tabs">
                            <button class="admin-tab active" data-tab="manage">${self.t('manageRequests') || 'Manage Requests'}<span class="admin-tab-badge" id="manageTabBadge" style="display:none !important;"></span></button>
                            <button class="admin-tab" data-tab="deletions">${self.t('deletionRequests') || 'Deletion Requests'}<span class="admin-tab-badge" id="deletionsTabBadge" style="display:none !important;"></span></button>
                        </div>
                        <div class="admin-tab-content" id="adminTabContent"></div>
                    `;
                    modalBody.innerHTML = tabsHtml;

                    // Update tab badges with pending counts
                    self.updateAdminTabBadges();

                    // Attach tab handlers
                    const tabs = modalBody.querySelectorAll('.admin-tab');
                    tabs.forEach(tab => {
                        tab.addEventListener('click', (e) => {
                            tabs.forEach(t => t.classList.remove('active'));
                            e.target.closest('.admin-tab').classList.add('active');
                            const tabName = e.target.closest('.admin-tab').getAttribute('data-tab');
                            if (tabName === 'deletions') {
                                self.renderDeletionRequestsTab(config);
                            } else {
                                self.renderAdminInterfaceInTab(config);
                            }
                        });
                    });

                    // Load manage tab by default
                    self.renderAdminInterfaceInTab(config);
                })
                .catch(() => {
                    self.renderAdminInterface(modalBody, {});
                });
        },

        /**
         * Render admin interface with config
         */
        renderAdminInterface: function (modalBody, config) {
            const self = this;
            const showLangSwitch = config.ShowLanguageSwitch !== false;

            // Fetch all requests
            this.fetchAllRequests().then(requests => {
                // Language switch HTML (only if enabled)
                const langSwitchHtml = showLangSwitch ? `
                    <div class="language-toggle-container" style="text-align:right;margin-bottom:10px;">
                        ${self.getLanguageSelectorHtml('languageToggle')}
                    </div>
                ` : '';

                // Build header with language switch only
                let html = langSwitchHtml;

                if (requests.length === 0) {
                    modalBody.innerHTML = html + '<div class="admin-request-empty">' + self.t('noRequestsYet') + '</div>';
                    // Attach language selector handler
                    const langToggle = document.getElementById('languageToggle');
                    if (langToggle) {
                        langToggle.addEventListener('change', () => {
                            self.setLanguage(langToggle.value);
                            self.loadAdminInterface();
                        });
                    }
                    return;
                }

                // Group requests by status: new (pending) > processing > snoozed > done > rejected
                const statusOrder = ['new', 'processing', 'snoozed', 'done', 'rejected'];
                const categoryLabels = {
                    new: self.t('categoryNew') || 'New',
                    processing: self.t('categoryProcessing'),
                    snoozed: self.t('categorySnoozed'),
                    done: self.t('categoryDone'),
                    rejected: self.t('categoryRejected')
                };

                // Categorize requests
                const categorized = {
                    new: [],
                    processing: [],
                    snoozed: [],
                    done: [],
                    rejected: []
                };

                requests.forEach(request => {
                    // Check if request is snoozed (has SnoozedUntil date in the future)
                    const isSnoozed = request.SnoozedUntil && new Date(request.SnoozedUntil) > new Date();

                    if (isSnoozed) {
                        categorized.snoozed.push(request);
                    } else if (request.Status === 'pending') {
                        // Pending = New (not yet viewed by admin)
                        categorized.new.push(request);
                    } else if (request.Status === 'processing') {
                        categorized.processing.push(request);
                    } else if (request.Status === 'done') {
                        categorized.done.push(request);
                    } else if (request.Status === 'rejected') {
                        categorized.rejected.push(request);
                    } else {
                        categorized.new.push(request); // Fallback
                    }
                });

                // Category icons for each status
                const categoryIcons = {
                    new: '🆕',
                    processing: '⚙️',
                    snoozed: '💤',
                    done: '✅',
                    rejected: '❌'
                };

                // Build HTML for each category (show all, even empty ones)
                statusOrder.forEach(status => {
                    const categoryRequests = categorized[status];

                    const icon = categoryIcons[status] || '📋';
                    html += `<div class="admin-category-section" data-category="${status}">`;
                    html += `<div class="admin-category-header">
                        <div class="admin-category-header-left">
                            <span class="admin-category-icon">${icon}</span>
                            <span>${categoryLabels[status]}</span>
                        </div>
                        <span class="admin-category-count">${categoryRequests.length}</span>
                        <span class="admin-category-chevron">▼</span>
                    </div>`;
                    html += `<div class="admin-category-content"><ul class="admin-category-list">`;

                    categoryRequests.forEach(request => {
                        html += self.renderAdminRequestItem(request, status === 'snoozed');
                    });

                    html += '</ul></div></div>';
                });

                modalBody.innerHTML = html;

                // Attach language toggle handler (only if it exists)
                const langToggle = document.getElementById('languageToggle');
                if (langToggle) {
                    langToggle.addEventListener('change', () => {
                        self.setLanguage(langToggle.value);
                        self.loadAdminInterface();
                    });
                }

                // Attach category header click handlers (expand/collapse)
                const categoryHeaders = modalBody.querySelectorAll('.admin-category-header');
                categoryHeaders.forEach(header => {
                    header.addEventListener('click', (e) => {
                        const section = header.closest('.admin-category-section');
                        if (section) {
                            section.classList.toggle('expanded');
                        }
                    });
                });

                // Attach request card click handlers (expand/collapse)
                const requestCompacts = modalBody.querySelectorAll('.admin-request-compact');
                requestCompacts.forEach(compact => {
                    compact.addEventListener('click', (e) => {
                        // Don't toggle if clicking on a button or link
                        if (e.target.closest('button') || e.target.closest('a')) return;

                        const item = compact.closest('.admin-request-item');
                        if (item) {
                            // Close other expanded items in the same category
                            const category = item.closest('.admin-category-section');
                            if (category) {
                                category.querySelectorAll('.admin-request-item.expanded').forEach(other => {
                                    if (other !== item) {
                                        other.classList.remove('expanded');
                                    }
                                });
                            }
                            item.classList.toggle('expanded');

                            // Auto-change pending requests to processing when expanded
                            if (item.classList.contains('expanded')) {
                                const requestId = item.getAttribute('data-request-id');
                                const category = item.closest('.admin-category-section');
                                if (requestId && category && category.getAttribute('data-category') === 'new') {
                                    self.autoProcessRequest(requestId);
                                }
                            }
                        }
                    });
                });

                // Prevent clicks inside the expanded panel from collapsing
                const detailsPanels = modalBody.querySelectorAll('.admin-request-details-panel');
                detailsPanels.forEach(panel => {
                    panel.addEventListener('click', (e) => {
                        e.stopPropagation();
                    });
                });

                // Attach status change handlers for buttons (desktop)
                const statusBtns = modalBody.querySelectorAll('.admin-status-btn');
                statusBtns.forEach(btn => {
                    btn.addEventListener('click', (e) => {
                        const requestId = e.target.getAttribute('data-request-id');
                        const newStatus = e.target.getAttribute('data-status');
                        // Get the media link if marking as done
                        const linkInput = modalBody.querySelector(`.admin-link-input[data-request-id="${requestId}"]`);
                        const mediaLink = linkInput ? linkInput.value.trim() : '';
                        // Get rejection reason if rejecting
                        const rejectionInput = modalBody.querySelector(`.admin-rejection-input[data-request-id="${requestId}"]`);
                        const rejectionReason = rejectionInput ? rejectionInput.value.trim() : '';
                        self.updateRequestStatus(requestId, newStatus, mediaLink, rejectionReason);
                    });
                });

                // Attach status change handlers for dropdown (mobile)
                const statusSelects = modalBody.querySelectorAll('.admin-status-select');
                statusSelects.forEach(select => {
                    select.addEventListener('change', (e) => {
                        const requestId = e.target.getAttribute('data-request-id');
                        const newStatus = e.target.value;
                        // Get the media link if marking as done
                        const linkInput = modalBody.querySelector(`.admin-link-input[data-request-id="${requestId}"]`);
                        const mediaLink = linkInput ? linkInput.value.trim() : '';
                        // Get rejection reason if rejecting
                        const rejectionInput = modalBody.querySelector(`.admin-rejection-input[data-request-id="${requestId}"]`);
                        const rejectionReason = rejectionInput ? rejectionInput.value.trim() : '';
                        self.updateRequestStatus(requestId, newStatus, mediaLink, rejectionReason);
                    });
                });

                // Attach snooze handlers
                const snoozeBtns = modalBody.querySelectorAll('.admin-snooze-btn');
                snoozeBtns.forEach(btn => {
                    btn.addEventListener('click', (e) => {
                        const requestId = e.target.getAttribute('data-request-id');
                        const dateInput = modalBody.querySelector(`.admin-snooze-date[data-request-id="${requestId}"]`);
                        if (dateInput && dateInput.value) {
                            self.snoozeRequestModal(requestId, dateInput.value);
                        } else {
                            if (window.require) {
                                require(['toast'], function(toast) {
                                    toast('Please select a snooze date');
                                });
                            }
                        }
                    });
                });

                // Attach unsnooze handlers
                const unsnoozeBtns = modalBody.querySelectorAll('.admin-unsnooze-btn');
                unsnoozeBtns.forEach(btn => {
                    btn.addEventListener('click', (e) => {
                        const requestId = e.target.getAttribute('data-request-id');
                        self.unsnoozeRequestModal(requestId);
                    });
                });

                // Attach delete handlers
                const deleteBtns = modalBody.querySelectorAll('.admin-delete-btn');
                deleteBtns.forEach(btn => {
                    btn.addEventListener('click', (e) => {
                        const requestId = e.target.getAttribute('data-request-id');
                        if (confirm(self.t('confirmDelete'))) {
                            self.deleteRequest(requestId);
                        }
                    });
                });
            }).catch(err => {
                console.error('Error loading requests:', err);
                modalBody.innerHTML = '<div class="admin-request-empty">' + self.t('errorLoading') + '</div>';
            });
        },

        /**
         * Snooze a request (modal version)
         */
        snoozeRequestModal: function (requestId, snoozedUntil) {
            const self = this;
            const baseUrl = ApiClient.serverAddress();
            const accessToken = ApiClient.accessToken();
            const deviceId = ApiClient.deviceId();
            const url = `${baseUrl}/Ratings/Requests/${requestId}/Snooze?snoozedUntil=${encodeURIComponent(snoozedUntil)}`;

            const authHeader = `MediaBrowser Client="Jellyfin Web", Device="Browser", DeviceId="${deviceId}", Version="10.11.0", Token="${accessToken}"`;

            fetch(url, {
                method: 'POST',
                credentials: 'include',
                headers: { 'X-Emby-Authorization': authHeader }
            })
            .then(response => {
                if (!response.ok) throw new Error('Failed to snooze');
                return response.json();
            })
            .then(() => {
                if (window.require) {
                    require(['toast'], function(toast) {
                        toast(self.t('statusUpdated') || 'Request snoozed');
                    });
                }
                self.loadAdminInterface();
            })
            .catch(err => {
                console.error('Error snoozing request:', err);
                if (window.require) {
                    require(['toast'], function(toast) {
                        toast('Error snoozing request');
                    });
                }
            });
        },

        /**
         * Unsnooze a request (modal version)
         */
        unsnoozeRequestModal: function (requestId) {
            const self = this;
            const baseUrl = ApiClient.serverAddress();
            const accessToken = ApiClient.accessToken();
            const deviceId = ApiClient.deviceId();
            const url = `${baseUrl}/Ratings/Requests/${requestId}/Unsnooze`;

            const authHeader = `MediaBrowser Client="Jellyfin Web", Device="Browser", DeviceId="${deviceId}", Version="10.11.0", Token="${accessToken}"`;

            fetch(url, {
                method: 'POST',
                credentials: 'include',
                headers: { 'X-Emby-Authorization': authHeader }
            })
            .then(response => {
                if (!response.ok) throw new Error('Failed to unsnooze');
                return response.json();
            })
            .then(() => {
                if (window.require) {
                    require(['toast'], function(toast) {
                        toast(self.t('statusUpdated') || 'Request unsnoozed');
                    });
                }
                self.loadAdminInterface();
            })
            .catch(err => {
                console.error('Error unsnoozing request:', err);
                if (window.require) {
                    require(['toast'], function(toast) {
                        toast('Error unsnoozing request');
                    });
                }
            });
        },

        /**
         * Submit a new media request
         */
        submitMediaRequest: function () {
            const self = this;
            try {
                const title = document.getElementById('requestMediaTitle').value.trim();
                const typeEl = document.getElementById('requestMediaType');
                const type = typeEl ? typeEl.value.trim() : '';
                const notesEl = document.getElementById('requestMediaNotes');
                const notes = notesEl ? notesEl.value.trim() : '';
                const imdbCodeEl = document.getElementById('requestImdbCode');
                const imdbCode = imdbCodeEl ? imdbCodeEl.value.trim() : '';
                const imdbLinkEl = document.getElementById('requestImdbLink');
                const imdbLink = imdbLinkEl ? imdbLinkEl.value.trim() : '';

                if (!title) {
                    if (window.require) {
                        const msg = self.t('titleRequired');
                        require(['toast'], function(toast) {
                            toast(msg);
                        });
                    }
                    return;
                }

                // Type validation - only if element exists and is required
                if (typeEl && typeEl.hasAttribute('required') && !type) {
                    if (window.require) {
                        const msg = self.t('typeRequired');
                        require(['toast'], function(toast) {
                            toast(msg);
                        });
                    }
                    return;
                }

                // IMDB Code validation - only if element exists and is required
                if (imdbCodeEl && imdbCodeEl.hasAttribute('required') && !imdbCode) {
                    if (window.require) {
                        require(['toast'], function(toast) {
                            toast('Please fill in: IMDB Code');
                        });
                    }
                    return;
                }

                // IMDB Link validation - only if element exists and is required
                if (imdbLinkEl && imdbLinkEl.hasAttribute('required') && !imdbLink) {
                    if (window.require) {
                        require(['toast'], function(toast) {
                            toast('Please fill in: IMDB Link');
                        });
                    }
                    return;
                }

                // Notes validation - only if element exists and is required
                if (notesEl && notesEl.hasAttribute('required') && !notes) {
                    if (window.require) {
                        require(['toast'], function(toast) {
                            toast('Please fill in: Notes');
                        });
                    }
                    return;
                }

                // Collect custom fields
                const customFieldInputs = document.querySelectorAll('[id^="customField_"]');
                const customFieldsObj = {};
                let customFieldsValid = true;
                customFieldInputs.forEach(input => {
                    const fieldName = input.getAttribute('data-field-name');
                    const value = input.value.trim();
                    if (input.hasAttribute('required') && !value) {
                        customFieldsValid = false;
                        if (window.require) {
                            require(['toast'], function(toast) {
                                toast(`Please fill in: ${fieldName}`);
                            });
                        }
                    }
                    if (fieldName && value) {
                        customFieldsObj[fieldName] = value;
                    }
                });

                if (!customFieldsValid) {
                    return;
                }

                const baseUrl = ApiClient.serverAddress();
                const accessToken = ApiClient.accessToken();
                const deviceId = ApiClient.deviceId();
                const url = `${baseUrl}/Ratings/Requests`;

                const authHeader = `MediaBrowser Client="Jellyfin Web", Device="Browser", DeviceId="${deviceId}", Version="10.11.0", Token="${accessToken}"`;

                const requestData = {
                    Title: title,
                    Type: type,
                    Notes: notes,
                    CustomFields: Object.keys(customFieldsObj).length > 0 ? JSON.stringify(customFieldsObj) : '',
                    ImdbCode: imdbCode,
                    ImdbLink: imdbLink
                };

                fetch(url, {
                    method: 'POST',
                    credentials: 'include',
                    headers: {
                        'Content-Type': 'application/json',
                        'X-Emby-Authorization': authHeader
                    },
                    body: JSON.stringify(requestData)
                })
                .then(response => {
                    if (!response.ok) {
                        throw new Error('Failed to submit request');
                    }
                    return response.json();
                })
                .then(data => {
                    if (window.require) {
                        const msg = self.t('requestSubmitted');
                        require(['toast'], function(toast) {
                            toast(msg);
                        });
                    }

                    // Clear form
                    document.getElementById('requestMediaTitle').value = '';
                    if (typeEl) typeEl.value = '';
                    if (notesEl) notesEl.value = '';
                    // Clear IMDB fields
                    if (imdbCodeEl) imdbCodeEl.value = '';
                    if (imdbLinkEl) imdbLinkEl.value = '';
                    // Clear custom fields
                    customFieldInputs.forEach(input => {
                        input.value = '';
                    });

                    // Reload user's request list to show the new request
                    self.loadUserRequests();
                })
                .catch(err => {
                    console.error('Error submitting request:', err);
                    if (window.require) {
                        const msg = self.t('requestFailed');
                        require(['toast'], function(toast) {
                            toast(msg);
                        });
                    }
                });
            } catch (err) {
                console.error('Error in submitMediaRequest:', err);
            }
        },

        /**
         * Fetch all media requests (admin only)
         */
        fetchAllRequests: function () {
            return new Promise((resolve, reject) => {
                try {
                    const baseUrl = ApiClient.serverAddress();
                    const accessToken = ApiClient.accessToken();
                    const deviceId = ApiClient.deviceId();
                    const url = `${baseUrl}/Ratings/Requests`;

                    const authHeader = `MediaBrowser Client="Jellyfin Web", Device="Browser", DeviceId="${deviceId}", Version="10.11.0", Token="${accessToken}"`;

                    fetch(url, {
                        method: 'GET',
                        credentials: 'include',
                        headers: {
                            'Content-Type': 'application/json',
                            'X-Emby-Authorization': authHeader
                        }
                    })
                    .then(response => {
                        if (!response.ok) {
                            throw new Error('Failed to fetch requests');
                        }
                        return response.json();
                    })
                    .then(requests => {
                        resolve(requests || []);
                    })
                    .catch(err => {
                        console.error('Error fetching requests:', err);
                        reject(err);
                    });
                } catch (err) {
                    console.error('Error in fetchAllRequests:', err);
                    reject(err);
                }
            });
        },

        /**
         * Fetch all deletion requests
         */
        fetchDeletionRequests: function () {
            return new Promise((resolve, reject) => {
                try {
                    const baseUrl = ApiClient.serverAddress();
                    const accessToken = ApiClient.accessToken();
                    const deviceId = ApiClient.deviceId();
                    const url = `${baseUrl}/Ratings/DeletionRequests`;

                    const authHeader = `MediaBrowser Client="Jellyfin Web", Device="Browser", DeviceId="${deviceId}", Version="10.11.0", Token="${accessToken}"`;

                    fetch(url, {
                        method: 'GET',
                        credentials: 'include',
                        headers: {
                            'Content-Type': 'application/json',
                            'X-Emby-Authorization': authHeader
                        }
                    })
                    .then(response => {
                        if (!response.ok) {
                            throw new Error('Failed to fetch deletion requests');
                        }
                        return response.json();
                    })
                    .then(requests => {
                        resolve(requests || []);
                    })
                    .catch(err => {
                        console.error('Error fetching deletion requests:', err);
                        resolve([]); // Don't reject, just return empty
                    });
                } catch (err) {
                    console.error('Error in fetchDeletionRequests:', err);
                    resolve([]);
                }
            });
        },

        /**
         * Extract Jellyfin item ID from a media link URL
         */
        extractItemIdFromLink: function (mediaLink) {
            if (!mediaLink) return null;
            try {
                // Try ?id=GUID
                const urlMatch = mediaLink.match(/[?&]id=([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})/);
                if (urlMatch) return urlMatch[1];

                // Try /Items/GUID
                const itemsMatch = mediaLink.match(/\/Items\/([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})/);
                if (itemsMatch) return itemsMatch[1];

                // Try #/details?id=GUID (Jellyfin web UI format)
                const detailsMatch = mediaLink.match(/details\?id=([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})/);
                if (detailsMatch) return detailsMatch[1];

                // Try raw GUID anywhere in the URL
                const rawMatch = mediaLink.match(/([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})/);
                if (rawMatch) return rawMatch[1];
            } catch (e) {
                console.error('Error extracting item ID from link:', e);
            }
            return null;
        },

        /**
         * Submit a deletion request
         */
        submitDeletionRequest: function (mediaRequestId, itemId, title, type, mediaLink, deletionType, btnElement) {
            const self = this;
            try {
                const baseUrl = ApiClient.serverAddress();
                const accessToken = ApiClient.accessToken();
                const deviceId = ApiClient.deviceId();
                const authHeader = `MediaBrowser Client="Jellyfin Web", Device="Browser", DeviceId="${deviceId}", Version="10.11.0", Token="${accessToken}"`;

                // Disable the button while submitting
                if (btnElement) {
                    btnElement.disabled = true;
                    btnElement.textContent = '...';
                }

                fetch(`${baseUrl}/Ratings/DeletionRequests`, {
                    method: 'POST',
                    credentials: 'include',
                    headers: {
                        'Content-Type': 'application/json',
                        'X-Emby-Authorization': authHeader
                    },
                    body: JSON.stringify({
                        MediaRequestId: mediaRequestId,
                        ItemId: itemId || '00000000-0000-0000-0000-000000000000',
                        Title: title,
                        Type: type,
                        MediaLink: mediaLink || '',
                        DeletionType: deletionType || 'media'
                    })
                })
                .then(response => {
                    if (!response.ok) {
                        return response.text().then(text => { throw new Error(text); });
                    }
                    return response.json();
                })
                .then(() => {
                    if (window.require) {
                        const msg = self.t('deletionRequestSent');
                        require(['toast'], function(toast) {
                            toast(msg);
                        });
                    }
                    // Reload the user requests to show updated state
                    self.loadUserRequests();
                    // Update badge
                    const requestBtn = document.querySelector('.request-media-button');
                    if (requestBtn) {
                        self.updateRequestBadge(requestBtn);
                    }
                })
                .catch(err => {
                    console.error('Error submitting deletion request:', err);
                    if (window.require) {
                        const msg = self.t('deletionRequestFailed');
                        require(['toast'], function(toast) {
                            toast(msg);
                        });
                    }
                    if (btnElement) {
                        btnElement.disabled = false;
                        btnElement.textContent = self.t('askToDelete');
                    }
                });
            } catch (err) {
                console.error('Error in submitDeletionRequest:', err);
            }
        },

        /**
         * Render deletion requests tab content (admin)
         */
        renderDeletionRequestsTab: function (config) {
            const self = this;
            const tabContent = document.getElementById('adminTabContent');
            if (!tabContent) return;

            tabContent.innerHTML = '<p style="text-align: center; color: #999;">' + this.t('loading') + '</p>';

            this.fetchDeletionRequests().then(requests => {
                if (requests.length === 0) {
                    tabContent.innerHTML = '<div class="admin-request-empty">' + self.t('noDeletionRequests') + '</div>';
                    return;
                }

                // Sort: pending first, then by date
                const sorted = requests.sort((a, b) => {
                    if (a.Status === 'pending' && b.Status !== 'pending') return -1;
                    if (a.Status !== 'pending' && b.Status === 'pending') return 1;
                    return new Date(b.CreatedAt) - new Date(a.CreatedAt);
                });

                let html = '<div class="deletion-requests-list">';
                sorted.forEach(request => {
                    const createdAt = request.CreatedAt ? self.formatDateTime(request.CreatedAt) : '';
                    const resolvedAt = request.ResolvedAt ? self.formatDateTime(request.ResolvedAt) : '';
                    const isPending = request.Status === 'pending';

                    let statusBadgeClass = request.Status;
                    let statusText = self.t('deletion' + request.Status.charAt(0).toUpperCase() + request.Status.slice(1)) || request.Status.toUpperCase();

                    let actionsHtml = '';
                    const isDeleteRequest = request.DeletionType === 'request';
                    const typeLabel = isDeleteRequest ? (self.t('deleteRequest')) : (self.t('deleteMedia'));
                    if (isPending) {
                        if (isDeleteRequest) {
                            // For "delete request" type: just Approve (deletes the request) or Reject
                            actionsHtml = `
                                <div class="deletion-request-actions">
                                    <button class="deletion-action-btn approve" data-request-id="${request.Id}" data-action="approve">${self.t('approveDeleteRequest')}</button>
                                    <button class="deletion-action-btn reject" data-request-id="${request.Id}" data-action="reject">${self.t('rejectDeletion')}</button>
                                </div>
                            `;
                        } else {
                            // For "delete media" type: schedule options
                            actionsHtml = `
                                <div class="deletion-request-actions">
                                    <button class="deletion-action-btn approve" data-request-id="${request.Id}" data-action="approve" data-delay-hours="1">${self.t('deleteNow')}</button>
                                    <button class="deletion-action-btn schedule" data-request-id="${request.Id}" data-action="approve" data-delay-days="1">${self.t('schedule1Day')}</button>
                                    <button class="deletion-action-btn schedule" data-request-id="${request.Id}" data-action="approve" data-delay-days="7">${self.t('schedule1Week')}</button>
                                    <button class="deletion-action-btn schedule" data-request-id="${request.Id}" data-action="approve" data-delay-days="30">${self.t('schedule1Month')}</button>
                                    <button class="deletion-action-btn reject" data-request-id="${request.Id}" data-action="reject">${self.t('rejectDeletion')}</button>
                                </div>
                            `;
                        }
                    }

                    let resolvedHtml = '';
                    if (!isPending && resolvedAt) {
                        resolvedHtml = `<span> • ${request.ResolvedByUsername ? self.escapeHtml(request.ResolvedByUsername) : ''} ${resolvedAt}</span>`;
                    }

                    let rejectionReasonHtml = '';
                    if (request.Status === 'rejected' && request.RejectionReason) {
                        rejectionReasonHtml = `<div style="margin-top:4px;font-size:11px;color:#f44336;">❌ ${self.t('rejectionReasonLabel')} ${self.escapeHtml(request.RejectionReason)}</div>`;
                    }

                    html += `
                        <div class="deletion-request-item ${isPending ? '' : 'resolved'}">
                            <div class="deletion-request-info">
                                <div class="deletion-request-title">${self.escapeHtml(request.Title)} <span style="font-size:10px;color:#999;font-weight:400;">[${typeLabel}]</span></div>
                                <div class="deletion-request-meta">
                                    <span class="deletion-request-user">${self.escapeHtml(request.Username)}</span>
                                    <span> • ${request.Type ? self.escapeHtml(request.Type) : ''}</span>
                                    <span> • ${createdAt}</span>
                                    ${resolvedHtml}
                                    ${request.MediaLink ? ` • <a href="${self.escapeHtml(request.MediaLink)}" class="deletion-request-link" target="_blank">▶ ${self.t('watchNow')}</a>` : ''}
                                </div>
                                ${rejectionReasonHtml}
                                ${actionsHtml}
                            </div>
                            <span class="deletion-status-badge ${statusBadgeClass}">${statusText}</span>
                        </div>
                    `;
                });
                html += '</div>';
                tabContent.innerHTML = html;

                // Attach action button handlers
                const actionBtns = tabContent.querySelectorAll('.deletion-action-btn');
                actionBtns.forEach(btn => {
                    btn.addEventListener('click', (e) => {
                        const target = e.target;
                        const requestId = target.getAttribute('data-request-id');
                        const action = target.getAttribute('data-action');
                        const delayDays = target.getAttribute('data-delay-days');
                        const delayHours = target.getAttribute('data-delay-hours');
                        self.processDeletionAction(requestId, action, delayDays, delayHours, config);
                    });
                });

                // Render ban section for deletion requests
                self.renderBanSection(tabContent, 'deletion_request', requests);
            }).catch(err => {
                console.error('Error loading deletion requests:', err);
                tabContent.innerHTML = '<p style="text-align: center; color: #f44336;">Error loading deletion requests</p>';
            });
        },

        /**
         * Process admin action on a deletion request
         */
        processDeletionAction: function (requestId, action, delayDays, delayHours, config) {
            const self = this;

            if (action === 'reject') {
                // Show custom modal for rejection reason
                self.showRejectionReasonModal(function (rejectionReason) {
                    self._executeDeletionAction(requestId, action, delayDays, delayHours, config, rejectionReason);
                });
            } else {
                self._executeDeletionAction(requestId, action, delayDays, delayHours, config, '');
            }
        },

        _executeDeletionAction: function (requestId, action, delayDays, delayHours, config, rejectionReason) {
            const self = this;
            try {
                const baseUrl = ApiClient.serverAddress();
                const accessToken = ApiClient.accessToken();
                const deviceId = ApiClient.deviceId();
                const authHeader = `MediaBrowser Client="Jellyfin Web", Device="Browser", DeviceId="${deviceId}", Version="10.11.0", Token="${accessToken}"`;

                let url = `${baseUrl}/Ratings/DeletionRequests/${requestId}/Action?action=${action}`;
                if (delayDays) url += `&delayDays=${delayDays}`;
                if (delayHours) url += `&delayHours=${delayHours}`;
                if (rejectionReason) url += `&rejectionReason=${encodeURIComponent(rejectionReason)}`;

                fetch(url, {
                    method: 'POST',
                    credentials: 'include',
                    headers: {
                        'Content-Type': 'application/json',
                        'X-Emby-Authorization': authHeader
                    }
                })
                .then(response => {
                    if (!response.ok) {
                        throw new Error('Failed to process action');
                    }
                    return response.json();
                })
                .then(() => {
                    // Refresh the tab
                    self.renderDeletionRequestsTab(config);
                    // Update badges
                    self.updateAdminTabBadges();
                    const requestBtn = document.querySelector('.request-media-button');
                    if (requestBtn) {
                        self.updateRequestBadge(requestBtn);
                    }
                })
                .catch(err => {
                    console.error('Error processing deletion action:', err);
                    if (window.require) {
                        const msg = self.t('deletionActionFailed');
                        require(['toast'], function(toast) {
                            toast(msg);
                        });
                    }
                });
            } catch (err) {
                console.error('Error in _executeDeletionAction:', err);
            }
        },

        showRejectionReasonModal: function (callback) {
            const self = this;
            // Remove any existing modal
            const existing = document.getElementById('rejectionReasonModal');
            if (existing) existing.remove();

            const overlay = document.createElement('div');
            overlay.id = 'rejectionReasonModal';
            overlay.className = 'rejection-modal-overlay';
            overlay.innerHTML = `
                <div class="rejection-modal">
                    <div class="rejection-modal-title">${self.t('rejectionReasonPrompt')}</div>
                    <textarea id="rejectionReasonInput" placeholder="${self.t('rejectionReasonLabel')}"></textarea>
                    <div class="rejection-modal-buttons">
                        <button class="rejection-modal-btn cancel" id="rejectionCancelBtn">${self.t('cancel') || 'Cancel'}</button>
                        <button class="rejection-modal-btn confirm" id="rejectionConfirmBtn">${self.t('rejectDeletion')}</button>
                    </div>
                </div>
            `;

            document.body.appendChild(overlay);

            // Focus textarea
            setTimeout(() => {
                const textarea = document.getElementById('rejectionReasonInput');
                if (textarea) textarea.focus();
            }, 100);

            // Cancel button
            document.getElementById('rejectionCancelBtn').addEventListener('click', () => {
                overlay.remove();
            });

            // Click outside to cancel
            overlay.addEventListener('click', (e) => {
                if (e.target === overlay) overlay.remove();
            });

            // Confirm button
            document.getElementById('rejectionConfirmBtn').addEventListener('click', () => {
                const reason = (document.getElementById('rejectionReasonInput').value || '').trim();
                overlay.remove();
                callback(reason);
            });
        },

        showRejectionReasonsPopup: function (reasons) {
            const self = this;
            const existing = document.getElementById('rejectionReasonPopup');
            if (existing) existing.remove();

            let itemsHtml = '';
            reasons.forEach((r, i) => {
                const date = r.date ? self.formatDateTime(r.date) : '';
                const admin = r.admin ? self.escapeHtml(r.admin) : '';
                const reason = r.reason ? self.escapeHtml(r.reason) : '-';
                itemsHtml += `
                    <div style="margin-bottom:${i < reasons.length - 1 ? '12px' : '0'} !important;">
                        <div style="font-size:11px !important;color:#999 !important;margin-bottom:4px !important;">${admin}${date ? ' • ' + date : ''}</div>
                        <div class="rejection-reason-popup-text">${reason}</div>
                    </div>
                `;
            });

            const overlay = document.createElement('div');
            overlay.id = 'rejectionReasonPopup';
            overlay.className = 'rejection-modal-overlay';
            overlay.innerHTML = `
                <div class="rejection-reason-popup">
                    <div class="rejection-reason-popup-title">${self.t('deletionRejected')} (${reasons.length})</div>
                    ${itemsHtml}
                    <button class="rejection-reason-popup-close">OK</button>
                </div>
            `;

            document.body.appendChild(overlay);

            overlay.querySelector('.rejection-reason-popup-close').addEventListener('click', () => {
                overlay.remove();
            });

            overlay.addEventListener('click', (e) => {
                if (e.target === overlay) overlay.remove();
            });
        },

        fetchBans: function (banType) {
            try {
                const baseUrl = ApiClient.serverAddress();
                const accessToken = ApiClient.accessToken();
                const deviceId = ApiClient.deviceId();
                const authHeader = `MediaBrowser Client="Jellyfin Web", Device="Browser", DeviceId="${deviceId}", Version="10.11.0", Token="${accessToken}"`;

                return fetch(`${baseUrl}/Ratings/Bans?banType=${banType}`, {
                    method: 'GET',
                    credentials: 'include',
                    headers: { 'X-Emby-Authorization': authHeader }
                })
                .then(r => r.ok ? r.json() : [])
                .catch(() => []);
            } catch (e) {
                return Promise.resolve([]);
            }
        },

        checkBan: function (banType) {
            try {
                const baseUrl = ApiClient.serverAddress();
                const accessToken = ApiClient.accessToken();
                const deviceId = ApiClient.deviceId();
                const authHeader = `MediaBrowser Client="Jellyfin Web", Device="Browser", DeviceId="${deviceId}", Version="10.11.0", Token="${accessToken}"`;

                return fetch(`${baseUrl}/Ratings/Bans/Check?banType=${banType}`, {
                    method: 'GET',
                    credentials: 'include',
                    headers: { 'X-Emby-Authorization': authHeader }
                })
                .then(r => r.ok ? r.json() : { banned: false })
                .catch(() => ({ banned: false }));
            } catch (e) {
                return Promise.resolve({ banned: false });
            }
        },

        createBan: function (userId, banType, duration) {
            const self = this;
            const baseUrl = ApiClient.serverAddress();
            const accessToken = ApiClient.accessToken();
            const deviceId = ApiClient.deviceId();
            const authHeader = `MediaBrowser Client="Jellyfin Web", Device="Browser", DeviceId="${deviceId}", Version="10.11.0", Token="${accessToken}"`;

            return fetch(`${baseUrl}/Ratings/Bans?userId=${userId}&banType=${banType}&duration=${duration}`, {
                method: 'POST',
                credentials: 'include',
                headers: { 'X-Emby-Authorization': authHeader }
            }).then(r => {
                if (!r.ok) throw new Error('Ban failed');
                return r.json();
            });
        },

        liftBan: function (banId) {
            const baseUrl = ApiClient.serverAddress();
            const accessToken = ApiClient.accessToken();
            const deviceId = ApiClient.deviceId();
            const authHeader = `MediaBrowser Client="Jellyfin Web", Device="Browser", DeviceId="${deviceId}", Version="10.11.0", Token="${accessToken}"`;

            return fetch(`${baseUrl}/Ratings/Bans/${banId}`, {
                method: 'DELETE',
                credentials: 'include',
                headers: { 'X-Emby-Authorization': authHeader }
            }).then(r => {
                if (!r.ok) throw new Error('Unban failed');
                return r.json();
            });
        },

        renderBanSection: function (container, banType, users) {
            const self = this;

            self.fetchBans(banType).then(bans => {
                let html = `<div class="ban-section">
                    <div class="ban-section-title">${self.t('bannedUsers')}</div>`;

                if (bans.length === 0) {
                    html += `<div style="font-size:11px;color:#666;margin-bottom:8px;">${self.t('noBannedUsers')}</div>`;
                } else {
                    bans.forEach(ban => {
                        const expires = ban.ExpiresAt
                            ? `${self.t('banExpires')} ${self.formatDateTime(ban.ExpiresAt)}`
                            : self.t('banPermanentLabel');
                        html += `
                            <div class="ban-item">
                                <div>
                                    <div class="ban-item-info">${self.escapeHtml(ban.Username)}</div>
                                    <div class="ban-item-meta">${expires} • ${self.t('bannedBy')} ${self.escapeHtml(ban.BannedByUsername)}</div>
                                </div>
                                <button class="ban-btn unban" data-ban-id="${ban.Id}">${self.t('unbanUser')}</button>
                            </div>
                        `;
                    });
                }

                // Ban new user dropdown - show unique usernames from requests
                if (users && users.length > 0) {
                    const bannedIds = new Set(bans.map(b => b.UserId));
                    const uniqueUsers = [];
                    const seen = new Set();
                    users.forEach(u => {
                        if (!seen.has(u.UserId) && !bannedIds.has(u.UserId)) {
                            seen.add(u.UserId);
                            uniqueUsers.push({ id: u.UserId, name: u.Username || u.UserId });
                        }
                    });

                    if (uniqueUsers.length > 0) {
                        html += `
                            <div style="margin-top:8px;display:flex;align-items:center;gap:6px;flex-wrap:wrap;">
                                <select class="ban-dropdown" id="banUserSelect_${banType}">
                                    ${uniqueUsers.map(u => `<option value="${u.id}">${self.escapeHtml(u.name)}</option>`).join('')}
                                </select>
                                <select class="ban-dropdown" id="banDuration_${banType}">
                                    <option value="1d">${self.t('ban1Day')}</option>
                                    <option value="1w">${self.t('ban1Week')}</option>
                                    <option value="1m">${self.t('ban1Month')}</option>
                                    <option value="permanent">${self.t('banPermanent')}</option>
                                </select>
                                <button class="ban-btn ban" id="banSubmit_${banType}">${self.t('banUser')}</button>
                            </div>
                        `;
                    }
                }

                html += '</div>';

                const banDiv = document.createElement('div');
                banDiv.innerHTML = html;
                container.appendChild(banDiv);

                // Attach unban handlers
                banDiv.querySelectorAll('.ban-btn.unban').forEach(btn => {
                    btn.addEventListener('click', () => {
                        const banId = btn.getAttribute('data-ban-id');
                        self.liftBan(banId).then(() => {
                            banDiv.remove();
                            self.renderBanSection(container, banType, users);
                        });
                    });
                });

                // Attach ban submit handler
                const submitBtn = banDiv.querySelector(`#banSubmit_${banType}`);
                if (submitBtn) {
                    submitBtn.addEventListener('click', () => {
                        const userId = banDiv.querySelector(`#banUserSelect_${banType}`).value;
                        const duration = banDiv.querySelector(`#banDuration_${banType}`).value;
                        self.createBan(userId, banType, duration).then(() => {
                            banDiv.remove();
                            self.renderBanSection(container, banType, users);
                        });
                    });
                }
            });
        },

        /**
         * Format date time for display
         */
        formatDateTime: function (dateString) {
            try {
                const date = new Date(dateString);
                return date.toLocaleDateString() + ' ' + date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
            } catch (e) {
                return dateString;
            }
        },

        /**
         * Delete a media request (admin only)
         */
        deleteRequest: function (requestId) {
            const self = this;
            try {
                const baseUrl = ApiClient.serverAddress();
                const accessToken = ApiClient.accessToken();
                const deviceId = ApiClient.deviceId();
                const url = `${baseUrl}/Ratings/Requests/${requestId}`;

                const authHeader = `MediaBrowser Client="Jellyfin Web", Device="Browser", DeviceId="${deviceId}", Version="10.11.0", Token="${accessToken}"`;

                fetch(url, {
                    method: 'DELETE',
                    credentials: 'include',
                    headers: {
                        'Content-Type': 'application/json',
                        'X-Emby-Authorization': authHeader
                    }
                })
                .then(response => {
                    if (!response.ok) {
                        throw new Error('Failed to delete request');
                    }
                    return response.json();
                })
                .then(data => {
                    if (window.require) {
                        require(['toast'], function(toast) {
                            toast('Request deleted');
                        });
                    }

                    // Reload the admin interface
                    self.loadAdminInterface();

                    // Update badge
                    const btn = document.getElementById('requestMediaBtn');
                    if (btn) {
                        self.updateRequestBadge(btn);
                    }
                })
                .catch(err => {
                    console.error('Error deleting request:', err);
                    if (window.require) {
                        require(['toast'], function(toast) {
                            toast('Error deleting request');
                        });
                    }
                });
            } catch (err) {
                console.error('Error in deleteRequest:', err);
            }
        },

        /**
         * Update request status (admin only)
         */
        updateRequestStatus: function (requestId, newStatus, mediaLink, rejectionReason) {
            const self = this;
            try {
                const baseUrl = ApiClient.serverAddress();
                const accessToken = ApiClient.accessToken();
                const deviceId = ApiClient.deviceId();
                let url = `${baseUrl}/Ratings/Requests/${requestId}/Status?status=${newStatus}`;

                // Add mediaLink if provided and status is done
                if (mediaLink && newStatus === 'done') {
                    url += `&mediaLink=${encodeURIComponent(mediaLink)}`;
                }

                // Add rejectionReason if provided and status is rejected
                if (rejectionReason && newStatus === 'rejected') {
                    url += `&rejectionReason=${encodeURIComponent(rejectionReason)}`;
                }

                const authHeader = `MediaBrowser Client="Jellyfin Web", Device="Browser", DeviceId="${deviceId}", Version="10.11.0", Token="${accessToken}"`;

                fetch(url, {
                    method: 'POST',
                    credentials: 'include',
                    headers: {
                        'Content-Type': 'application/json',
                        'X-Emby-Authorization': authHeader
                    }
                })
                .then(response => {
                    if (!response.ok) {
                        throw new Error('Failed to update status');
                    }
                    return response.json();
                })
                .then(data => {
                    if (window.require) {
                        const msg = self.t('statusUpdated') + ': ' + self.t(newStatus);
                        require(['toast'], function(toast) {
                            toast(msg);
                        });
                    }

                    // Reload the admin interface
                    self.loadAdminInterface();

                    // Update tab badges
                    self.updateAdminTabBadges();

                    // Update badge to reflect new counts
                    const btn = document.getElementById('requestMediaBtn');
                    if (btn) {
                        self.updateRequestBadge(btn);
                    }
                })
                .catch(err => {
                    console.error('Error updating status:', err);
                    if (window.require) {
                        const msg = self.t('statusUpdateFailed');
                        require(['toast'], function(toast) {
                            toast(msg);
                        });
                    }
                });
            } catch (err) {
                console.error('Error in updateRequestStatus:', err);
            }
        },

        /**
         * Update notification badge on button
         */
        updateRequestBadge: function (btn) {
            const self = this;
            try {
                Promise.all([
                    this.fetchAllRequests(),
                    this.fetchDeletionRequests()
                ]).then(([requests, deletionRequests]) => {
                    // Check if user is admin
                    this.checkIfAdmin().then(isAdmin => {
                        let count = 0;

                        if (isAdmin) {
                            // For admin: show count of pending requests + pending deletion requests
                            count = requests.filter(r => r.Status === 'pending').length;
                            count += deletionRequests.filter(r => r.Status === 'pending').length;
                        } else {
                            // For users: show count of completed (done) requests they haven't seen yet
                            const userId = ApiClient.getCurrentUserId();
                            const userRequests = requests.filter(r => r.UserId === userId);
                            const doneRequests = userRequests.filter(r => r.Status === 'done');

                            // Get viewed request IDs from localStorage
                            const viewedRequests = self.getViewedRequestIds();

                            // Count only done requests that haven't been viewed
                            count = doneRequests.filter(r => !viewedRequests.includes(r.Id)).length;
                        }

                        // Remove existing badge
                        const existingBadge = btn.querySelector('.request-badge');
                        if (existingBadge) {
                            existingBadge.remove();
                        }

                        // Add badge if count > 0
                        if (count > 0) {
                            const badge = document.createElement('span');
                            badge.className = 'request-badge';
                            badge.textContent = count;
                            btn.appendChild(badge);
                        }
                    }).catch(err => {
                        console.error('Error checking admin status for badge:', err);
                    });
                }).catch(err => {
                    console.error('Error updating request badge:', err);
                });
            } catch (err) {
                console.error('Error in updateRequestBadge:', err);
            }
        },

        /**
         * Update badges on admin tabs (Manage / Deletion Requests)
         */
        updateAdminTabBadges: function () {
            Promise.all([
                this.fetchAllRequests(),
                this.fetchDeletionRequests()
            ]).then(([requests, deletionRequests]) => {
                const pendingRequests = requests.filter(r => r.Status === 'pending').length;
                const pendingDeletions = deletionRequests.filter(r => r.Status === 'pending').length;

                const manageBadge = document.getElementById('manageTabBadge');
                if (manageBadge) {
                    if (pendingRequests > 0) {
                        manageBadge.textContent = pendingRequests;
                        manageBadge.style.cssText = 'display:inline-flex !important;';
                    } else {
                        manageBadge.style.cssText = 'display:none !important;';
                    }
                }

                const deletionsBadge = document.getElementById('deletionsTabBadge');
                if (deletionsBadge) {
                    if (pendingDeletions > 0) {
                        deletionsBadge.textContent = pendingDeletions;
                        deletionsBadge.style.cssText = 'display:inline-flex !important;';
                    } else {
                        deletionsBadge.style.cssText = 'display:none !important;';
                    }
                }
            }).catch(() => {});
        },

        /**
         * Get list of viewed request IDs from localStorage
         */
        getViewedRequestIds: function () {
            try {
                const stored = localStorage.getItem('ratingsPlugin_viewedRequests');
                return stored ? JSON.parse(stored) : [];
            } catch (err) {
                console.error('Error reading viewed requests:', err);
                return [];
            }
        },

        /**
         * Mark all current done requests as viewed
         */
        markDoneRequestsAsViewed: function () {
            const self = this;
            try {
                this.fetchAllRequests().then(requests => {
                    const userId = ApiClient.getCurrentUserId();
                    const userRequests = requests.filter(r => r.UserId === userId);
                    const doneRequests = userRequests.filter(r => r.Status === 'done');

                    // Get current viewed list
                    const viewedIds = self.getViewedRequestIds();

                    // Add all done request IDs
                    doneRequests.forEach(request => {
                        if (!viewedIds.includes(request.Id)) {
                            viewedIds.push(request.Id);
                        }
                    });

                    // Save back to localStorage
                    localStorage.setItem('ratingsPlugin_viewedRequests', JSON.stringify(viewedIds));

                    // Update badge immediately to reflect changes
                    const btn = document.getElementById('requestMediaBtn');
                    if (btn) {
                        self.updateRequestBadge(btn);
                    }
                }).catch(err => {
                    console.error('Error marking requests as viewed:', err);
                });
            } catch (err) {
                console.error('Error in markDoneRequestsAsViewed:', err);
            }
        },

        // ============================================
        // NEW MEDIA NOTIFICATIONS
        // ============================================

        /**
         * Notification state
         * Session-based: only shows notifications for media added AFTER user login
         */
        notificationsEnabled: false,
        lastNotificationCheck: null,
        notificationPollingInterval: null,
        shownNotificationIds: [],
        notificationSessionUserId: null, // Track which user session this is for

        /**
         * SessionStorage keys for notification persistence
         */
        NOTIFICATION_KEYS: {
            SESSION_START: 'ratingsNotificationSessionStart',    // When user logged in (ISO timestamp)
            SHOWN_IDS: 'ratingsShownNotificationIds',            // Array of shown notification IDs
            LAST_CHECK: 'ratingsLastNotificationCheck',          // Last poll timestamp
            SESSION_USER: 'ratingsNotificationSessionUser'       // User ID for this session
        },

        /**
         * Load notification session from sessionStorage
         */
        loadNotificationSession: function () {
            try {
                const currentUserId = window.ApiClient ? ApiClient.getCurrentUserId() : null;
                const storedUserId = sessionStorage.getItem(this.NOTIFICATION_KEYS.SESSION_USER);

                // If user changed, clear session data
                if (currentUserId && storedUserId && currentUserId !== storedUserId) {
                    this.clearNotificationSession();
                    return null;
                }

                // Load shown notification IDs
                const shownIdsJson = sessionStorage.getItem(this.NOTIFICATION_KEYS.SHOWN_IDS);
                if (shownIdsJson) {
                    try {
                        this.shownNotificationIds = JSON.parse(shownIdsJson);
                    } catch (e) {
                        this.shownNotificationIds = [];
                    }
                }

                // Load last check time
                const lastCheck = sessionStorage.getItem(this.NOTIFICATION_KEYS.LAST_CHECK);
                if (lastCheck) {
                    this.lastNotificationCheck = lastCheck;
                }

                // Load session start time
                const sessionStart = sessionStorage.getItem(this.NOTIFICATION_KEYS.SESSION_START);

                this.notificationSessionUserId = currentUserId;

                return sessionStart;
            } catch (err) {
                console.error('RatingsPlugin: Error loading notification session:', err);
                return null;
            }
        },

        /**
         * Save notification session to sessionStorage
         */
        saveNotificationSession: function () {
            try {
                const currentUserId = window.ApiClient ? ApiClient.getCurrentUserId() : null;
                if (currentUserId) {
                    sessionStorage.setItem(this.NOTIFICATION_KEYS.SESSION_USER, currentUserId);
                }
                sessionStorage.setItem(this.NOTIFICATION_KEYS.SHOWN_IDS, JSON.stringify(this.shownNotificationIds));
                if (this.lastNotificationCheck) {
                    sessionStorage.setItem(this.NOTIFICATION_KEYS.LAST_CHECK, this.lastNotificationCheck);
                }
            } catch (err) {
                console.error('RatingsPlugin: Error saving notification session:', err);
            }
        },

        /**
         * Clear notification session data (on logout or user change)
         */
        clearNotificationSession: function () {
            try {
                sessionStorage.removeItem(this.NOTIFICATION_KEYS.SESSION_START);
                sessionStorage.removeItem(this.NOTIFICATION_KEYS.SHOWN_IDS);
                sessionStorage.removeItem(this.NOTIFICATION_KEYS.LAST_CHECK);
                sessionStorage.removeItem(this.NOTIFICATION_KEYS.SESSION_USER);
                this.shownNotificationIds = [];
                this.lastNotificationCheck = null;
                this.notificationSessionUserId = null;
            } catch (err) {
                console.error('RatingsPlugin: Error clearing notification session:', err);
            }
        },

        /**
         * Start a new notification session (on login)
         */
        startNotificationSession: function () {
            try {
                const currentUserId = window.ApiClient ? ApiClient.getCurrentUserId() : null;
                if (!currentUserId) {
                    return;
                }

                const now = new Date().toISOString();
                sessionStorage.setItem(this.NOTIFICATION_KEYS.SESSION_START, now);
                sessionStorage.setItem(this.NOTIFICATION_KEYS.SESSION_USER, currentUserId);
                sessionStorage.setItem(this.NOTIFICATION_KEYS.SHOWN_IDS, '[]');
                sessionStorage.setItem(this.NOTIFICATION_KEYS.LAST_CHECK, now);

                this.lastNotificationCheck = now;
                this.shownNotificationIds = [];
                this.notificationSessionUserId = currentUserId;

            } catch (err) {
                console.error('RatingsPlugin: Error starting notification session:', err);
            }
        },

        /**
         * Initialize notifications system
         * Uses session-based tracking: only shows notifications for media added AFTER user login
         */
        initNotifications: function () {
            const self = this;

            // Check if user is logged in
            const currentUserId = window.ApiClient ? ApiClient.getCurrentUserId() : null;
            if (!currentUserId) {
                return;
            }

            // Check if notifications are enabled in config
            this.checkNotificationsEnabled().then(enabled => {
                self.notificationsEnabled = enabled;
                if (enabled) {
                    // Create notification container
                    self.createNotificationContainer();

                    // Load existing session or start new one
                    const sessionStart = self.loadNotificationSession();

                    if (sessionStart) {
                        // Existing session - use stored timestamp
                        // lastNotificationCheck was already loaded in loadNotificationSession
                    } else {
                        // New session - start from NOW (not 5 minutes ago!)
                        // This prevents old notifications from appearing
                        self.startNotificationSession();
                    }

                    // Start polling for notifications
                    self.startNotificationPolling();

                    // Admin test button disabled - use TV app for testing
                    // self.initTestNotificationButton();
                }
            });
        },

        /**
         * Check if notifications are enabled
         */
        checkNotificationsEnabled: function () {
            return new Promise((resolve) => {
                try {
                    if (!window.ApiClient) {
                        resolve(false);
                        return;
                    }

                    const baseUrl = ApiClient.serverAddress();
                    fetch(`${baseUrl}/Ratings/Config`, {
                        method: 'GET',
                        credentials: 'include'
                    })
                        .then(response => response.json())
                        .then(config => {
                            resolve(config.EnableNewMediaNotifications === true);
                        })
                        .catch(() => {
                            resolve(false);
                        });
                } catch (err) {
                    resolve(false);
                }
            });
        },

        /**
         * Create notification container
         */
        createNotificationContainer: function () {
            if (document.getElementById('ratingsNotificationContainer')) {
                return;
            }

            const container = document.createElement('div');
            container.id = 'ratingsNotificationContainer';
            container.className = 'ratings-notification-container';
            document.body.appendChild(container);
        },

        /**
         * Start polling for new notifications
         */
        startNotificationPolling: function () {
            const self = this;

            // Poll every 10 seconds
            this.notificationPollingInterval = setInterval(() => {
                self.checkForNewNotifications();
            }, 10000);

            // Also check immediately
            this.checkForNewNotifications();
        },

        /**
         * Check for new notifications from server
         * Only polls when user is logged in, persists state to sessionStorage
         */
        checkForNewNotifications: function () {
            const self = this;

            // Check if user has disabled notifications via toggle
            if (this.userNotificationsEnabled === false) {
                return;
            }

            if (!window.ApiClient) {
                return;
            }

            // Check if user is logged in
            const currentUserId = ApiClient.getCurrentUserId();
            if (!currentUserId) {
                this.stopNotificationPolling();
                return;
            }

            // Verify user hasn't changed mid-session
            if (this.notificationSessionUserId && this.notificationSessionUserId !== currentUserId) {
                this.clearNotificationSession();
                this.startNotificationSession();
            }

            const baseUrl = ApiClient.serverAddress();
            // Use session start time as fallback - NEVER use 5 minutes ago
            const since = this.lastNotificationCheck || new Date().toISOString();


            fetch(`${baseUrl}/Ratings/Notifications?since=${encodeURIComponent(since)}`, {
                method: 'GET',
                credentials: 'include'
            })
                .then(response => {
                    if (!response.ok) {
                        throw new Error('HTTP ' + response.status);
                    }
                    return response.json();
                })
                .then(notifications => {

                    if (notifications && notifications.length > 0) {
                        notifications.forEach(notification => {
                            // Don't show duplicates
                            if (!self.shownNotificationIds.includes(notification.Id)) {
                                self.shownNotificationIds.push(notification.Id);
                                self.showNotification(notification);
                            } else {
                            }
                        });
                    }

                    // Update last check time
                    self.lastNotificationCheck = new Date().toISOString();

                    // Persist session state to sessionStorage
                    self.saveNotificationSession();
                })
                .catch(err => {
                    console.error('RatingsPlugin: Error checking for notifications:', err);
                });
        },

        /**
         * Stop notification polling
         */
        stopNotificationPolling: function () {
            if (this.notificationPollingInterval) {
                clearInterval(this.notificationPollingInterval);
                this.notificationPollingInterval = null;
            }
        },

        /**
         * Show a notification
         */
        showNotification: function (notification) {
            const container = document.getElementById('ratingsNotificationContainer');
            if (!container) return;

            const baseUrl = window.ApiClient ? ApiClient.serverAddress() : '';

            // Create notification element
            const notifEl = document.createElement('div');
            notifEl.className = 'ratings-notification' + (notification.IsTest ? ' test-notification' : '');
            notifEl.setAttribute('data-notification-id', notification.Id);

            // Build image URL
            let imageHtml = '';
            if (notification.ImageUrl && !notification.IsTest) {
                imageHtml = `<img class="ratings-notification-image" src="${baseUrl}${notification.ImageUrl}" alt="" onerror="this.style.display='none'">`;
            }

            // Build content based on notification type
            let contentHtml = '';
            if (notification.IsTest) {
                contentHtml = `
                    <div class="ratings-notification-content">
                        <div class="ratings-notification-header">
                            <span class="ratings-notification-icon">🔔</span>
                            <span class="ratings-notification-label">Test Notification</span>
                        </div>
                        <div class="ratings-notification-message">${this.escapeHtml(notification.Message || 'Test notification')}</div>
                    </div>
                `;
            } else {
                const yearText = notification.Year ? ` (${notification.Year})` : '';
                let typeLabel, titleText, icon;

                if (notification.MediaType === 'Movie') {
                    typeLabel = 'New Movie Available';
                    titleText = this.escapeHtml(notification.Title) + yearText;
                    icon = '🎬';
                } else if (notification.MediaType === 'Episode') {
                    const seriesName = notification.SeriesName ? this.escapeHtml(notification.SeriesName) : 'Series';
                    const seasonNum = notification.SeasonNumber;
                    const seasonText = (seasonNum !== null && seasonNum !== undefined && seasonNum > 0)
                        ? ` S${seasonNum.toString().padStart(2, '0')}` : '';
                    typeLabel = seriesName + seasonText + yearText;

                    // Handle grouped episode notifications
                    if (notification.EpisodeNumbers && notification.EpisodeNumbers.length > 1) {
                        const episodeDisplay = this.formatEpisodeRange(notification.EpisodeNumbers);
                        titleText = `New episodes: ${episodeDisplay}`;
                    } else {
                        const episodeNum = notification.EpisodeNumber;
                        // Check for valid episode number (not null, not undefined, not 0)
                        titleText = (episodeNum !== null && episodeNum !== undefined && episodeNum > 0)
                            ? `Episode ${episodeNum} is available`
                            : 'New episode available';
                    }
                    icon = '📺';
                } else {
                    typeLabel = 'New Series Available';
                    titleText = this.escapeHtml(notification.Title) + yearText;
                    icon = '📺';
                }

                contentHtml = `
                    <div class="ratings-notification-content">
                        <div class="ratings-notification-header">
                            <span class="ratings-notification-icon">${icon}</span>
                            <span class="ratings-notification-label">${typeLabel}</span>
                        </div>
                        <div class="ratings-notification-title">${titleText}</div>
                    </div>
                `;
            }

            notifEl.innerHTML = `
                ${imageHtml}
                ${contentHtml}
                <button class="ratings-notification-close" title="Dismiss">&times;</button>
            `;

            // Add close button handler
            const closeBtn = notifEl.querySelector('.ratings-notification-close');
            closeBtn.addEventListener('click', () => {
                this.hideNotification(notifEl);
            });

            // Add click handler to navigate to item (if not a test)
            if (!notification.IsTest && notification.ItemId && notification.ItemId !== '00000000-0000-0000-0000-000000000000') {
                notifEl.style.cursor = 'pointer';
                notifEl.addEventListener('click', (e) => {
                    if (e.target !== closeBtn && !closeBtn.contains(e.target)) {
                        window.location.hash = `#/details?id=${notification.ItemId}`;
                        this.hideNotification(notifEl);
                    }
                });
            }

            // Add to container
            container.appendChild(notifEl);

            // Auto-hide after 8 seconds
            setTimeout(() => {
                this.hideNotification(notifEl);
            }, 8000);
        },

        /**
         * Format episode numbers into a readable range (e.g., "4-8" or "1, 3, 5")
         */
        formatEpisodeRange: function (episodeNumbers) {
            if (!episodeNumbers || episodeNumbers.length === 0) return '';
            if (episodeNumbers.length === 1) return episodeNumbers[0].toString();

            // Sort episodes
            const sorted = [...episodeNumbers].sort((a, b) => a - b);

            // Check if consecutive
            let isConsecutive = true;
            for (let i = 1; i < sorted.length; i++) {
                if (sorted[i] !== sorted[i - 1] + 1) {
                    isConsecutive = false;
                    break;
                }
            }

            if (isConsecutive) {
                return `${sorted[0]}-${sorted[sorted.length - 1]}`;
            } else {
                return sorted.join(', ');
            }
        },

        /**
         * Hide a notification with animation
         */
        hideNotification: function (notifEl) {
            if (!notifEl || !notifEl.parentNode) return;

            notifEl.classList.add('hiding');
            setTimeout(() => {
                if (notifEl.parentNode) {
                    notifEl.remove();
                }
            }, 300);
        },

        /**
         * Initialize admin test notification button
         */
        initTestNotificationButton: function () {
            const self = this;

            // Don't show on login page
            if (this.isOnLoginPage()) return;

            // Check if user is admin first
            this.checkIfAdmin().then(isAdmin => {
                if (!isAdmin) {
                    // Remove button if exists and user is not admin
                    const existingBtn = document.getElementById('testNotificationBtn');
                    if (existingBtn) {
                        existingBtn.remove();
                    }
                    return;
                }

                // Don't create if already exists
                if (document.getElementById('testNotificationBtn')) return;

                const header = document.querySelector('.skinHeader') || document.querySelector('header');
                if (!header) return;

                const btn = document.createElement('button');
                btn.id = 'testNotificationBtn';
                btn.innerHTML = '🔔 Test';
                btn.title = 'Send a test notification to all users';

                btn.addEventListener('click', () => {
                    self.sendTestNotification();
                });

                header.appendChild(btn);

                // Hide on login page - check periodically
                setInterval(() => {
                    try {
                        const testBtn = document.getElementById('testNotificationBtn');
                        if (!testBtn) return;

                        const isLoginPage = self.isOnLoginPage();
                        const videoPlayer = document.querySelector('.videoPlayerContainer');
                        const isVideoPlaying = videoPlayer && !videoPlayer.classList.contains('hide');

                        if (isLoginPage || isVideoPlaying) {
                            testBtn.style.display = 'none';
                        } else {
                            testBtn.style.display = '';
                        }
                    } catch (err) {
                        // Silently fail
                    }
                }, 500);
            });
        },

        /**
         * Send a test notification
         */
        sendTestNotification: function () {
            const self = this;

            if (!window.ApiClient) return;

            const baseUrl = ApiClient.serverAddress();
            const accessToken = ApiClient.accessToken();

            let deviceId = localStorage.getItem('_deviceId2');
            if (!deviceId) {
                deviceId = 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function (c) {
                    const r = Math.random() * 16 | 0;
                    const v = c === 'x' ? r : (r & 0x3 | 0x8);
                    return v.toString(16);
                });
                localStorage.setItem('_deviceId2', deviceId);
            }

            const authHeader = `MediaBrowser Client="Jellyfin Web", Device="Browser", DeviceId="${deviceId}", Version="10.11.0", Token="${accessToken}"`;

            fetch(`${baseUrl}/Ratings/Notifications/Test`, {
                method: 'POST',
                credentials: 'include',
                headers: {
                    'Content-Type': 'application/json',
                    'X-Emby-Authorization': authHeader
                }
            })
                .then(response => {
                    if (response.ok) {
                        if (window.require) {
                            require(['toast'], function (toast) {
                                toast('Test notification sent!');
                            });
                        }
                        // Check for notifications immediately
                        setTimeout(() => {
                            self.checkForNewNotifications();
                        }, 500);
                    } else {
                        throw new Error('Failed to send test notification');
                    }
                })
                .catch(err => {
                    console.error('Error sending test notification:', err);
                    if (window.require) {
                        require(['toast'], function (toast) {
                            toast('Error sending test notification');
                        });
                    }
                });
        },

        /**
         * Escape HTML to prevent XSS
         */
        escapeHtml: function (text) {
            const div = document.createElement('div');
            div.textContent = text;
            return div.innerHTML;
        },

        /**
         * Netflix View Configuration
         */
        netflixViewEnabled: false,
        netflixViewInitialized: false,

        /**
         * Initialize Netflix-style view
         */
        initNetflixView: function () {
            const self = this;

            // Check if feature is enabled via API
            this.checkNetflixViewEnabled().then(enabled => {
                self.netflixViewEnabled = enabled;
                if (enabled) {
                    self.observeLibraryPages();
                }
            });
        },

        /**
         * Check if Netflix view is enabled in plugin config
         */
        checkNetflixViewEnabled: function () {
            return new Promise((resolve) => {
                try {
                    if (!window.ApiClient) {
                        resolve(false);
                        return;
                    }

                    const baseUrl = ApiClient.serverAddress();
                    const url = `${baseUrl}/Ratings/Config`;

                    fetch(url, {
                        method: 'GET',
                        credentials: 'include'
                    })
                    .then(response => response.json())
                    .then(config => {
                        resolve(config.EnableNetflixView === true);
                    })
                    .catch(() => {
                        resolve(false);
                    });
                } catch (err) {
                    resolve(false);
                }
            });
        },

        /**
         * Check if current page should show Netflix view
         */
        isNetflixViewPage: function () {
            const hash = window.location.hash;
            const isMoviesPage = hash.includes('#/movies') || hash.includes('collectionType=movies');
            const isTVPage = hash.includes('#/tv') || hash.includes('collectionType=tvshows');
            const hasTopParentId = hash.includes('topParentId=');
            return hasTopParentId && (isMoviesPage || isTVPage);
        },

        /**
         * Observe library pages for Netflix view
         */
        observeLibraryPages: function () {
            const self = this;
            let lastUrl = '';
            let transformTimeout = null;
            let hideStyleElement = null;

            // Inject CSS to instantly hide default content on Netflix pages
            const injectHideStyles = () => {
                if (self.isNetflixViewPage() && !hideStyleElement) {
                    hideStyleElement = document.createElement('style');
                    hideStyleElement.id = 'netflix-view-hide-default';
                    hideStyleElement.textContent = `
                        .itemsContainer:not(.netflix-view-active),
                        .vertical-list:not(.netflix-view-active) {
                            opacity: 0 !important;
                            pointer-events: none !important;
                        }
                    `;
                    document.head.appendChild(hideStyleElement);
                }
            };

            // Remove hide styles when not on Netflix page
            const removeHideStyles = () => {
                if (hideStyleElement) {
                    hideStyleElement.remove();
                    hideStyleElement = null;
                }
            };

            const checkLibraryPage = () => {
                const url = window.location.href;
                const hash = window.location.hash;
                const shouldTransform = self.isNetflixViewPage();

                // Clean up Netflix view when navigating away
                if (!shouldTransform) {
                    removeHideStyles();
                    const existingNetflix = document.querySelector('.netflix-view-container');
                    if (existingNetflix) {
                        // Show original content again
                        const itemsContainer = document.querySelector('.itemsContainer');
                        if (itemsContainer) {
                            itemsContainer.style.display = '';
                        }
                        existingNetflix.remove();

                        // Reset header and main content styles
                        const skinHeader = document.querySelector('.skinHeader');
                        if (skinHeader) {
                            skinHeader.style.cssText = '';
                        }
                        const mainAnimatedPages = document.querySelector('.mainAnimatedPages, .view');
                        if (mainAnimatedPages) {
                            mainAnimatedPages.style.cssText = '';
                        }

                        // Restore body and html scrolling
                        document.body.style.overflow = '';
                        document.documentElement.style.overflow = '';
                    }
                    lastUrl = url;
                    return;
                }

                // Don't re-process same URL
                if (url === lastUrl && document.querySelector('.netflix-view-container')) {
                    return;
                }
                lastUrl = url;
                // Clear any pending transform
                if (transformTimeout) {
                    clearTimeout(transformTimeout);
                }

                // Inject hide styles immediately
                injectHideStyles();

                // Remove old Netflix view if exists (for re-navigation)
                const existingNetflix = document.querySelector('.netflix-view-container');
                if (existingNetflix) {
                    existingNetflix.remove();
                }

                // Use MutationObserver to detect when itemsContainer appears
                const tryTransform = () => {
                    const itemsContainer = document.querySelector('.itemsContainer') ||
                                           document.querySelector('.vertical-list');

                    if (itemsContainer) {                        removeHideStyles();
                        self.transformToNetflixView();
                        return true;
                    }
                    return false;
                };

                // Try immediately
                if (tryTransform()) return;

                // Watch for DOM changes
                const observer = new MutationObserver((mutations, obs) => {
                    if (tryTransform()) {
                        obs.disconnect();
                    }
                });

                observer.observe(document.body, {
                    childList: true,
                    subtree: true
                });

                // Fallback timeout
                transformTimeout = setTimeout(() => {
                    observer.disconnect();
                    removeHideStyles();
                    if (!document.querySelector('.netflix-view-container')) {
                        self.transformToNetflixView();
                    }
                }, 3000);
            };

            // Listen for hash changes (SPA navigation)
            window.addEventListener('hashchange', () => {                // Small delay to let Jellyfin start loading new page
                setTimeout(checkLibraryPage, 100);
            });

            // Also watch for popstate (back/forward navigation)
            window.addEventListener('popstate', () => {                setTimeout(checkLibraryPage, 100);
            });

            // Periodic check as fallback (less frequent)
            setInterval(checkLibraryPage, 2000);

            // Initial check
            setTimeout(checkLibraryPage, 500);
        },

        /**
         * Transform library page to Netflix-style view
         */
        transformToNetflixView: function () {
            const self = this;
            // Don't transform if already done
            if (document.querySelector('.netflix-view-container')) {                return;
            }

            // Find the main content area - try multiple selectors
            // Jellyfin uses different containers depending on navigation method
            let itemsContainer = document.querySelector('.itemsContainer');
            if (!itemsContainer) {
                itemsContainer = document.querySelector('.vertical-list');
            }
            if (!itemsContainer) {
                itemsContainer = document.querySelector('.view-inner');
            }
            if (!itemsContainer) {
                itemsContainer = document.querySelector('[data-role="content"] .padded-left');
            }
            if (!itemsContainer) {
                itemsContainer = document.querySelector('.libraryPage');
            }
            if (!itemsContainer) {
                // Try finding any scrollable content area
                itemsContainer = document.querySelector('.page:not(.hide) .content-primary');
            }
            if (!itemsContainer) {                // Retry after a delay - content may still be loading
                setTimeout(() => {
                    if (!document.querySelector('.netflix-view-container')) {
                        self.transformToNetflixView();
                    }
                }, 500);
                return;
            }

            // Get parent library ID from URL
            const parentId = this.getParentIdFromUrl();
            if (!parentId) {                return;
            }
            // Fix the header to stay at top when Netflix view is active
            const skinHeader = document.querySelector('.skinHeader');
            if (skinHeader) {
                skinHeader.style.cssText = `
                    position: fixed !important;
                    top: 0 !important;
                    left: 0 !important;
                    right: 0 !important;
                    z-index: 1000 !important;
                    background: #101010 !important;
                `;
            }

            // Hide body and html scrollbars - only Netflix container should scroll
            document.body.style.overflow = 'hidden';
            document.documentElement.style.overflow = 'hidden';

            // Also ensure main content area doesn't scroll
            const mainAnimatedPages = document.querySelector('.mainAnimatedPages, .view');
            if (mainAnimatedPages) {
                mainAnimatedPages.style.cssText = `
                    margin-top: 56px !important;
                    overflow: hidden !important;
                `;
            }

            // Create Netflix view container as a FIXED overlay
            const netflixContainer = document.createElement('div');
            netflixContainer.className = 'netflix-view-container';
            // Use fixed positioning to overlay below header - this ensures visibility
            netflixContainer.style.cssText = `
                display: block !important;
                visibility: visible !important;
                opacity: 1 !important;
                position: fixed !important;
                top: 56px !important;
                left: 0 !important;
                right: 0 !important;
                bottom: 0 !important;
                width: 100% !important;
                overflow-y: auto !important;
                background: #141414 !important;
                z-index: 100 !important;
            `;
            netflixContainer.innerHTML = '<div class="netflix-loading" style="color: white; text-align: center; padding: 50px; font-size: 18px;">Loading genres...</div>';

            // Insert directly into body as fixed overlay
            document.body.appendChild(netflixContainer);
            // Fetch genres and build view
            this.fetchGenresAndBuildView(parentId, netflixContainer);
        },

        /**
         * Get parent library ID from URL
         */
        getParentIdFromUrl: function () {
            const hash = window.location.hash;
            // Match various GUID formats (with or without dashes)
            const match = hash.match(/[?&]parentId=([a-f0-9-]+)/i) ||
                          hash.match(/[?&]topParentId=([a-f0-9-]+)/i) ||
                          hash.match(/parentId=([a-f0-9-]+)/i) ||
                          hash.match(/topParentId=([a-f0-9-]+)/i);
            return match ? match[1] : null;
        },

        /**
         * Fetch genres and build Netflix-style view
         */
        fetchGenresAndBuildView: function (parentId, container) {
            const self = this;
            const baseUrl = ApiClient.serverAddress();
            const accessToken = ApiClient.accessToken();
            const deviceId = ApiClient.deviceId();
            const authHeader = `MediaBrowser Client="Jellyfin Web", Device="Browser", DeviceId="${deviceId}", Version="10.11.0", Token="${accessToken}"`;

            const fetchUrl = `${baseUrl}/Items?ParentId=${parentId}&IncludeItemTypes=Movie,Series&Recursive=true&Fields=Genres,PrimaryImageAspectRatio&EnableTotalRecordCount=true&Limit=500`;
            // Get all items to extract genres
            fetch(fetchUrl, {
                method: 'GET',
                credentials: 'include',
                headers: {
                    'Content-Type': 'application/json',
                    'X-Emby-Authorization': authHeader
                }
            })
            .then(response => {                return response.json();
            })
            .then(data => {                const items = data.Items || [];

                // Extract unique genres
                const genreMap = new Map();
                items.forEach(item => {
                    if (item.Genres) {
                        item.Genres.forEach(genre => {
                            if (!genreMap.has(genre)) {
                                genreMap.set(genre, []);
                            }
                            genreMap.get(genre).push(item);
                        });
                    }
                });

                // Sort genres by number of items (most popular first)
                const sortedGenres = Array.from(genreMap.entries())
                    .sort((a, b) => b[1].length - a[1].length)
                    .slice(0, 15); // Limit to top 15 genres
                if (sortedGenres.length === 0) {
                    container.innerHTML = '<div class="netflix-loading" style="color: white; padding: 50px; text-align: center;">No genres found</div>';
                    return;
                }

                // Shuffle function for randomizing items within each genre
                const shuffleArray = (array) => {
                    const shuffled = [...array];
                    for (let i = shuffled.length - 1; i > 0; i--) {
                        const j = Math.floor(Math.random() * (i + 1));
                        [shuffled[i], shuffled[j]] = [shuffled[j], shuffled[i]];
                    }
                    return shuffled;
                };

                // Build Netflix view HTML with shuffled items per genre
                let html = '';
                sortedGenres.forEach(([genre, genreItems]) => {
                    const shuffledItems = shuffleArray(genreItems);
                    html += self.buildGenreRow(genre, shuffledItems, baseUrl);
                });                container.innerHTML = html;
                // Make sure container is visible
                container.style.display = 'block';

                // Attach scroll button handlers
                self.attachScrollHandlers(container);

                // Apply rating badges to Netflix cards
                self.applyNetflixRatingBadges(container);

                // Apply leaving badges to Netflix cards
                self.applyNetflixLeavingBadges(container);
            })
            .catch(err => {
                console.error('Error fetching items for Netflix view:', err);
                container.innerHTML = '<div class="netflix-loading">Error loading content</div>';
            });
        },

        /**
         * Build HTML for a genre row
         */
        buildGenreRow: function (genre, items, baseUrl) {
            const self = this;

            // Limit to 20 items per row
            const rowItems = items.slice(0, 20);

            let cardsHtml = '';
            rowItems.forEach(item => {
                const imageUrl = item.ImageTags && item.ImageTags.Primary
                    ? `${baseUrl}/Items/${item.Id}/Images/Primary?fillHeight=450&fillWidth=300&quality=96`
                    : `${baseUrl}/Items/${item.Id}/Images/Primary?fillHeight=450&fillWidth=300`;

                const itemUrl = `#!/details?id=${item.Id}`;

                cardsHtml += `
                    <a href="${itemUrl}" class="netflix-card" data-item-id="${item.Id}">
                        <img src="${imageUrl}" alt="${this.escapeHtml(item.Name)}" loading="lazy" onerror="this.src='data:image/svg+xml,<svg xmlns=%22http://www.w3.org/2000/svg%22 viewBox=%220 0 300 450%22><rect fill=%22%232a2a2a%22 width=%22300%22 height=%22450%22/><text x=%22150%22 y=%22225%22 fill=%22%23666%22 text-anchor=%22middle%22 font-size=%2220%22>No Image</text></svg>'">
                        <div class="netflix-card-overlay">
                            <div class="netflix-card-title">${this.escapeHtml(item.Name)}</div>
                            <div class="netflix-card-rating">${item.CommunityRating ? '★ ' + item.CommunityRating.toFixed(1) : ''}</div>
                        </div>
                    </a>
                `;
            });

            return `
                <div class="netflix-genre-row">
                    <div class="netflix-genre-title">${this.escapeHtml(genre)}</div>
                    <div class="netflix-row-wrapper">
                        <button class="netflix-scroll-btn left" aria-label="Scroll left">‹</button>
                        <div class="netflix-row-content">
                            ${cardsHtml}
                        </div>
                        <button class="netflix-scroll-btn right" aria-label="Scroll right">›</button>
                    </div>
                </div>
            `;
        },

        /**
         * Attach scroll button handlers
         */
        attachScrollHandlers: function (container) {
            const rows = container.querySelectorAll('.netflix-row-wrapper');

            rows.forEach(row => {
                const content = row.querySelector('.netflix-row-content');
                const leftBtn = row.querySelector('.netflix-scroll-btn.left');
                const rightBtn = row.querySelector('.netflix-scroll-btn.right');

                if (leftBtn && content) {
                    leftBtn.addEventListener('click', () => {
                        content.scrollBy({ left: -600, behavior: 'smooth' });
                    });
                }

                if (rightBtn && content) {
                    rightBtn.addEventListener('click', () => {
                        content.scrollBy({ left: 600, behavior: 'smooth' });
                    });
                }
            });
        },

        /**
         * Apply rating badges to Netflix cards
         */
        applyNetflixRatingBadges: function (container) {
            const self = this;
            const cards = container.querySelectorAll('.netflix-card[data-item-id]');
            cards.forEach(card => {
                const itemId = card.getAttribute('data-item-id');
                if (!itemId) return;

                // Check cache first
                if (self.ratingsCache[itemId] !== undefined) {
                    if (self.ratingsCache[itemId] !== null) {
                        const stats = self.ratingsCache[itemId];
                        card.classList.add('has-rating');
                        card.setAttribute('data-rating', '★ ' + stats.AverageRating.toFixed(1));
                    }
                    return;
                }

                // Fetch rating from API
                const baseUrl = ApiClient.serverAddress();
                const accessToken = ApiClient.accessToken();
                const url = `${baseUrl}/Ratings/Items/${itemId}/Stats`;

                let deviceId = localStorage.getItem('_deviceId2');
                if (!deviceId) {
                    deviceId = 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function(c) {
                        const r = Math.random() * 16 | 0;
                        const v = c === 'x' ? r : (r & 0x3 | 0x8);
                        return v.toString(16);
                    });
                    localStorage.setItem('_deviceId2', deviceId);
                }

                const authHeader = `MediaBrowser Client="Jellyfin Web", Device="Browser", DeviceId="${deviceId}", Version="10.11.0", Token="${accessToken}"`;

                fetch(url, {
                    method: 'GET',
                    credentials: 'include',
                    headers: {
                        'Content-Type': 'application/json',
                        'X-Emby-Authorization': authHeader
                    }
                })
                    .then(response => {
                        if (!response.ok) throw new Error(`HTTP error! status: ${response.status}`);
                        return response.json();
                    })
                    .then(stats => {
                        if (stats.TotalRatings > 0) {
                            self.ratingsCache[itemId] = stats;
                            card.classList.add('has-rating');
                            card.setAttribute('data-rating', '★ ' + stats.AverageRating.toFixed(1));
                        } else {
                            self.ratingsCache[itemId] = null;
                        }
                    })
                    .catch(() => {
                        self.ratingsCache[itemId] = null;
                    });
            });
        },

        /**
         * Apply leaving badges to Netflix cards
         */
        applyNetflixLeavingBadges: function (container) {
            const self = this;

            // Skip if no cache yet
            if (!self.scheduledDeletionsCache) {
                return;
            }

            const cards = container.querySelectorAll('.netflix-card[data-item-id]');
            cards.forEach(card => {
                const itemId = card.getAttribute('data-item-id');
                if (!itemId) return;

                // Check if this item has scheduled deletion
                const deletion = self.scheduledDeletionsCache[itemId.toLowerCase()];
                if (deletion) {
                    const leavingText = self.formatLeavingText(deletion.DeleteAt);
                    card.classList.add('has-leaving');
                    card.setAttribute('data-leaving', leavingText);
                }
            });
        },

        // ============ LIVE CHAT FUNCTIONS ============

        /**
         * Get authentication headers for API calls
         */
        getChatAuthHeaders: function () {
            const headers = { 'Content-Type': 'application/json' };
            if (window.ApiClient && ApiClient._serverInfo) {
                const token = ApiClient._serverInfo.AccessToken;
                if (token) {
                    headers['X-Emby-Authorization'] = 'MediaBrowser Client="Jellyfin Web", Device="Browser", DeviceId="' + (ApiClient._deviceId || 'unknown') + '", Version="10.11.0", Token="' + token + '"';
                }
            }
            return headers;
        },

        /**
         * Initialize chat with retry logic
         */
        initChatWithRetry: function () {
            const self = this;
            let attempts = 0;
            const maxAttempts = 15;

            const tryInit = function () {
                attempts++;
                if (!window.ApiClient) {
                    if (attempts < maxAttempts) {
                        setTimeout(tryInit, 1000);
                    }
                    return;
                }

                const baseUrl = ApiClient.serverAddress();
                fetch(baseUrl + '/Ratings/Config', { method: 'GET', credentials: 'include' })
                    .then(function (r) { return r.json(); })
                    .then(function (config) {
                        self.chatEnabled = config.EnableChat === true;
                        self.chatConfig = {
                            tenorApiKey: config.TenorApiKey || '',
                            allowGifs: config.ChatAllowGifs !== false,
                            allowEmojis: config.ChatAllowEmojis !== false,
                            maxMessageLength: config.ChatMaxMessageLength || 500,
                            rateLimitPerMinute: config.ChatRateLimitPerMinute || 10
                        };
                        if (self.chatEnabled) {
                            self.initChat();
                        }
                    })
                    .catch(function () {
                        self.chatEnabled = false;
                    });
            };

            setTimeout(tryInit, 1000);
        },

        /**
         * Initialize chat system
         */
        initChat: function () {
            const self = this;

            // Find and replace cast button
            this.injectChatButton();

            // Create chat window (hidden by default)
            this.createChatWindow();

            // Start heartbeat for presence
            this.startChatHeartbeat();

            // Check ban status
            this.checkChatBanStatus();
        },

        /**
         * Inject chat button in place of cast button
         */
        injectChatButton: function () {
            const self = this;
            let attempts = 0;

            const tryInject = function () {
                attempts++;
                const castBtn = document.querySelector('.headerCastButton');

                if (castBtn && !document.getElementById('chatBtn')) {
                    // Create chat button
                    const chatBtn = document.createElement('button');
                    chatBtn.id = 'chatBtn';
                    chatBtn.className = castBtn.className.replace('headerCastButton', '').replace('castButton', '');
                    chatBtn.innerHTML = '<span id="chatBtnIcon">💬</span><span class="chat-badge hidden" id="chatBadge">0</span>';
                    chatBtn.title = self.t('liveChat');
                    chatBtn.onclick = function () {
                        self.toggleChat();
                    };

                    // Insert before or replace cast button
                    castBtn.parentNode.insertBefore(chatBtn, castBtn);
                    castBtn.style.display = 'none';

                    return;
                }

                if (attempts < 30) {
                    setTimeout(tryInject, 1000);
                }
            };

            setTimeout(tryInject, 2000);
        },

        /**
         * Create chat window HTML
         */
        createChatWindow: function () {
            const self = this;

            if (document.getElementById('chatWindow')) return;

            const chatHtml = `
                <div id="chatWindow">
                    <div class="chat-header">
                        <div class="chat-header-left">
                            <span class="chat-header-title">${this.t('liveChat')}</span>
                            <span class="chat-online-count">
                                <span class="chat-online-dot"></span>
                                <span id="chatOnlineCount">0</span> ${this.t('chatOnline')}
                            </span>
                        </div>
                        <div class="chat-header-right">
                            <button class="chat-header-btn" id="chatSettingsBtn" title="Settings" style="display:none;">⚙️</button>
                            <button class="chat-header-btn" id="chatCloseBtn" title="Close">✕</button>
                        </div>
                    </div>
                    <div class="chat-admin-panel" id="chatAdminPanel">
                        <div class="chat-admin-section">
                            <div class="chat-admin-title">${this.t('chatModerators')}</div>
                            <div class="chat-admin-list" id="chatModeratorList"></div>
                        </div>
                        <div class="chat-admin-section">
                            <div class="chat-admin-title">${this.t('chatBannedUsers')}</div>
                            <div class="chat-admin-list" id="chatBannedList"></div>
                        </div>
                        <div class="chat-admin-section" id="chatClearSection" style="display:none;">
                            <button class="chat-admin-btn danger" id="chatClearAllBtn" style="width:100%;">${this.t('chatClearAll')}</button>
                        </div>
                    </div>
                    <div class="chat-messages" id="chatMessages">
                        <div class="chat-empty" id="chatEmpty">
                            <div class="chat-empty-icon">💬</div>
                            <div>${this.t('chatNoMessages')}</div>
                        </div>
                    </div>
                    <div class="chat-typing" id="chatTyping"></div>
                    <div class="chat-status" id="chatStatus" style="display:none;"></div>
                    <div class="chat-input-area" id="chatInputArea">
                        <div class="chat-emoji-picker" id="chatEmojiPicker">
                            <div class="chat-emoji-categories">
                                <button class="chat-emoji-category-btn active" data-category="smileys">😀</button>
                                <button class="chat-emoji-category-btn" data-category="gestures">👍</button>
                                <button class="chat-emoji-category-btn" data-category="hearts">❤️</button>
                                <button class="chat-emoji-category-btn" data-category="objects">🎉</button>
                            </div>
                            <div class="chat-emoji-list" id="chatEmojiList"></div>
                        </div>
                        <div class="chat-gif-picker" id="chatGifPicker">
                            <div class="chat-gif-search">
                                <input type="text" id="chatGifSearch" placeholder="${this.t('chatSearchGif')}">
                            </div>
                            <div class="chat-gif-list" id="chatGifList"></div>
                            <div class="chat-gif-powered">${this.t('chatPoweredBy')}</div>
                        </div>
                        <div class="chat-input-row">
                            <div class="chat-input-wrapper">
                                <textarea class="chat-input" id="chatInput" placeholder="${this.t('chatPlaceholder')}" rows="1"></textarea>
                                <div class="chat-input-btns">
                                    <button class="chat-emoji-btn" id="chatEmojiBtn" title="Emojis">😊</button>
                                    <button class="chat-gif-btn" id="chatGifBtn" title="GIFs" style="${this.chatConfig.allowGifs && this.chatConfig.tenorApiKey ? '' : 'display:none;'}">GIF</button>
                                </div>
                            </div>
                            <button class="chat-send-btn" id="chatSendBtn" title="${this.t('chatSend')}">➤</button>
                        </div>
                    </div>
                </div>
            `;

            const div = document.createElement('div');
            div.innerHTML = chatHtml;
            document.body.appendChild(div.firstElementChild);

            // Bind events
            this.bindChatEvents();

            // Load initial emojis
            this.loadEmojiCategory('smileys');
        },

        /**
         * Bind chat event handlers
         */
        bindChatEvents: function () {
            const self = this;

            // Close button
            document.getElementById('chatCloseBtn').onclick = function () {
                self.toggleChat();
            };

            // Settings button
            document.getElementById('chatSettingsBtn').onclick = function () {
                const panel = document.getElementById('chatAdminPanel');
                panel.classList.toggle('visible');
                if (panel.classList.contains('visible')) {
                    self.loadAdminPanel();
                }
            };

            // Send button
            document.getElementById('chatSendBtn').onclick = function () {
                self.sendChatMessage();
            };

            // Input events
            const input = document.getElementById('chatInput');
            input.onkeydown = function (e) {
                if (e.key === 'Enter' && !e.shiftKey) {
                    e.preventDefault();
                    self.sendChatMessage();
                }
            };
            input.oninput = function () {
                self.notifyTyping();
                // Auto-resize
                this.style.height = 'auto';
                this.style.height = Math.min(this.scrollHeight, 100) + 'px';
            };

            // Emoji picker toggle
            document.getElementById('chatEmojiBtn').onclick = function () {
                const picker = document.getElementById('chatEmojiPicker');
                const gifPicker = document.getElementById('chatGifPicker');
                gifPicker.classList.remove('visible');
                picker.classList.toggle('visible');
            };

            // Emoji category buttons
            document.querySelectorAll('.chat-emoji-category-btn').forEach(function (btn) {
                btn.onclick = function () {
                    document.querySelectorAll('.chat-emoji-category-btn').forEach(b => b.classList.remove('active'));
                    this.classList.add('active');
                    self.loadEmojiCategory(this.dataset.category);
                };
            });

            // GIF picker toggle
            if (this.chatConfig.allowGifs && this.chatConfig.tenorApiKey) {
                document.getElementById('chatGifBtn').onclick = function () {
                    const picker = document.getElementById('chatGifPicker');
                    const emojiPicker = document.getElementById('chatEmojiPicker');
                    emojiPicker.classList.remove('visible');
                    picker.classList.toggle('visible');
                    if (picker.classList.contains('visible')) {
                        self.loadTrendingGifs();
                    }
                };

                // GIF search
                let gifSearchTimeout;
                document.getElementById('chatGifSearch').oninput = function () {
                    clearTimeout(gifSearchTimeout);
                    const query = this.value.trim();
                    gifSearchTimeout = setTimeout(function () {
                        if (query) {
                            self.searchGifs(query);
                        } else {
                            self.loadTrendingGifs();
                        }
                    }, 500);
                };
            }

            // Clear all button (admin only)
            document.getElementById('chatClearAllBtn').onclick = function () {
                if (confirm(self.t('chatClearConfirm'))) {
                    self.clearAllChatMessages();
                }
            };

            // Close pickers when clicking outside
            document.addEventListener('click', function (e) {
                if (!e.target.closest('#chatEmojiPicker') && !e.target.closest('#chatEmojiBtn')) {
                    document.getElementById('chatEmojiPicker').classList.remove('visible');
                }
                if (!e.target.closest('#chatGifPicker') && !e.target.closest('#chatGifBtn')) {
                    document.getElementById('chatGifPicker').classList.remove('visible');
                }
                if (!e.target.closest('#chatAdminPanel') && !e.target.closest('#chatSettingsBtn')) {
                    document.getElementById('chatAdminPanel').classList.remove('visible');
                }
            });
        },

        /**
         * Toggle chat window
         */
        toggleChat: function () {
            const chatWindow = document.getElementById('chatWindow');
            this.chatOpen = !this.chatOpen;

            if (this.chatOpen) {
                chatWindow.classList.add('visible');
                this.startChatPolling();
                this.loadChatMessages();
                this.loadOnlineUsers();
                // Mark as read
                this.updateUnreadBadge(0);
            } else {
                chatWindow.classList.remove('visible');
                this.stopChatPolling();
            }
        },

        /**
         * Start chat message polling
         */
        startChatPolling: function () {
            const self = this;
            this.stopChatPolling();
            this.chatPollingInterval = setInterval(function () {
                self.loadChatMessages();
                self.loadOnlineUsers();
            }, 2000);
        },

        /**
         * Stop chat polling
         */
        stopChatPolling: function () {
            if (this.chatPollingInterval) {
                clearInterval(this.chatPollingInterval);
                this.chatPollingInterval = null;
            }
        },

        /**
         * Start heartbeat for presence
         */
        startChatHeartbeat: function () {
            const self = this;
            const heartbeat = function () {
                if (!window.ApiClient) return;
                const baseUrl = ApiClient.serverAddress();

                // Get current user's admin status from client
                let isAdmin = false;
                try {
                    const currentUser = ApiClient.getCurrentUser ? ApiClient.getCurrentUser() : null;
                    if (currentUser && currentUser.then) {
                        // It's a promise
                        currentUser.then(function (user) {
                            isAdmin = user?.Policy?.IsAdministrator || false;
                            self.sendHeartbeat(baseUrl, isAdmin);
                        }).catch(function () {
                            self.sendHeartbeat(baseUrl, false);
                        });
                        return;
                    } else if (currentUser) {
                        isAdmin = currentUser.Policy?.IsAdministrator || false;
                    }
                } catch (e) {}

                self.sendHeartbeat(baseUrl, isAdmin);
            };
            heartbeat();
            setInterval(heartbeat, 30000);
        },

        /**
         * Send heartbeat to server
         */
        sendHeartbeat: function (baseUrl, isAdmin) {
            const self = this;
            fetch(baseUrl + '/Ratings/Chat/Heartbeat', {
                method: 'POST',
                credentials: 'include',
                headers: self.getChatAuthHeaders(),
                body: JSON.stringify({ isAdmin: isAdmin })
            })
            .then(function (r) { return r.json(); })
            .then(function (data) {
                self.chatIsAdmin = data.isAdmin || false;
                self.chatIsModerator = data.isModerator || false;
                // Show settings button for admin/mod
                const settingsBtn = document.getElementById('chatSettingsBtn');
                if (settingsBtn) {
                    settingsBtn.style.display = (self.chatIsAdmin || self.chatIsModerator) ? '' : 'none';
                }
                // Show clear all button for admin
                const clearSection = document.getElementById('chatClearSection');
                if (clearSection) {
                    clearSection.style.display = self.chatIsAdmin ? '' : 'none';
                }
            })
            .catch(function () {});
        },

        /**
         * Check ban status
         */
        checkChatBanStatus: function () {
            const self = this;
            if (!window.ApiClient) return;
            const baseUrl = ApiClient.serverAddress();
            fetch(baseUrl + '/Ratings/Chat/BanStatus', {
                method: 'GET',
                credentials: 'include',
                headers: this.getChatAuthHeaders()
            })
            .then(function (r) { return r.json(); })
            .then(function (data) {
                self.chatBanStatus = data;
                self.updateChatInputState();
            })
            .catch(function () {});
        },

        /**
         * Update chat input state based on ban
         */
        updateChatInputState: function () {
            const inputArea = document.getElementById('chatInputArea');
            const status = document.getElementById('chatStatus');
            const input = document.getElementById('chatInput');

            if (this.chatBanStatus && this.chatBanStatus.chatBan) {
                inputArea.style.display = 'none';
                status.style.display = 'block';
                status.className = 'chat-status error';
                status.textContent = this.t('chatBanned');
            } else if (this.chatBanStatus && this.chatBanStatus.snoozeBan) {
                inputArea.style.display = 'none';
                status.style.display = 'block';
                status.className = 'chat-status warning';
                status.textContent = this.t('chatMuted');
            } else {
                inputArea.style.display = '';
                status.style.display = 'none';
                input.disabled = false;
            }
        },

        /**
         * Load chat messages
         */
        loadChatMessages: function () {
            const self = this;
            if (!window.ApiClient) return;
            const baseUrl = ApiClient.serverAddress();
            fetch(baseUrl + '/Ratings/Chat/Messages?limit=50', {
                method: 'GET',
                credentials: 'include',
                headers: this.getChatAuthHeaders()
            })
            .then(function (r) { return r.json(); })
            .then(function (data) {
                self.chatMessages = data.messages || [];
                self.chatTypingUsers = data.typingUsers || [];
                self.renderChatMessages();
                self.renderTypingIndicator();
            })
            .catch(function () {});
        },

        /**
         * Load online users
         */
        loadOnlineUsers: function () {
            const self = this;
            if (!window.ApiClient) return;
            const baseUrl = ApiClient.serverAddress();
            fetch(baseUrl + '/Ratings/Chat/Users/Online', {
                method: 'GET',
                credentials: 'include',
                headers: this.getChatAuthHeaders()
            })
            .then(function (r) { return r.json(); })
            .then(function (users) {
                self.chatUsers = users || [];
                const countEl = document.getElementById('chatOnlineCount');
                if (countEl) {
                    countEl.textContent = users.length;
                }
            })
            .catch(function () {});
        },

        /**
         * Render chat messages
         */
        renderChatMessages: function () {
            const container = document.getElementById('chatMessages');
            const empty = document.getElementById('chatEmpty');
            const self = this;

            if (this.chatMessages.length === 0) {
                empty.style.display = '';
                return;
            }

            empty.style.display = 'none';

            // Get current user ID for own messages
            const currentUserId = ApiClient._serverInfo?.UserId || '';

            let html = '';
            this.chatMessages.forEach(function (msg) {
                const isOwn = msg.userId === currentUserId;
                const avatarContent = msg.userAvatar
                    ? '<img src="' + msg.userAvatar + '" alt="">'
                    : msg.userName.charAt(0).toUpperCase();
                const roleClass = msg.isAdmin ? 'admin' : (msg.isModerator ? 'moderator' : '');
                const timeStr = self.formatChatTime(msg.timestamp);

                if (msg.isDeleted) {
                    html += '<div class="chat-message' + (isOwn ? ' own' : '') + '">'
                        + '<div class="chat-avatar">' + avatarContent + '</div>'
                        + '<div class="chat-message-content">'
                        + '<div class="chat-message-header">'
                        + '<span class="chat-username ' + roleClass + '">' + self.escapeHtml(msg.userName) + '</span>'
                        + '<span class="chat-timestamp">' + timeStr + '</span>'
                        + '</div>'
                        + '<div class="chat-message-deleted">' + self.t('chatDeleted') + '</div>'
                        + '</div></div>';
                } else {
                    html += '<div class="chat-message' + (isOwn ? ' own' : '') + '" data-message-id="' + msg.id + '">'
                        + '<div class="chat-avatar">' + avatarContent + '</div>'
                        + '<div class="chat-message-content">'
                        + '<div class="chat-message-header">'
                        + '<span class="chat-username ' + roleClass + '">' + (isOwn ? self.t('chatYou') : self.escapeHtml(msg.userName)) + '</span>'
                        + '<span class="chat-timestamp">' + timeStr + '</span>'
                        + '</div>'
                        + '<div class="chat-message-text">' + self.escapeHtml(msg.content) + '</div>'
                        + (msg.gifUrl ? '<img class="chat-message-gif" src="' + msg.gifUrl + '" alt="GIF">' : '');

                    // Add delete button for own messages or if moderator/admin
                    if (isOwn || self.chatIsAdmin || self.chatIsModerator) {
                        html += '<div class="chat-message-actions">'
                            + '<button class="chat-action-btn delete" onclick="RatingsPlugin.deleteChatMessage(\'' + msg.id + '\')">' + self.t('delete') + '</button>';
                        // Add ban option for admin/mod on other users
                        if (!isOwn && (self.chatIsAdmin || self.chatIsModerator)) {
                            html += '<button class="chat-action-btn" onclick="RatingsPlugin.showBanUserDialog(\'' + msg.userId + '\', \'' + self.escapeHtml(msg.userName) + '\')">' + self.t('chatBan') + '</button>';
                        }
                        html += '</div>';
                    }

                    html += '</div></div>';
                }
            });

            container.innerHTML = html;

            // Scroll to bottom
            container.scrollTop = container.scrollHeight;

            // Update last seen message
            if (this.chatMessages.length > 0) {
                this.chatLastMessageId = this.chatMessages[this.chatMessages.length - 1].id;
            }
        },

        /**
         * Render typing indicator
         */
        renderTypingIndicator: function () {
            const el = document.getElementById('chatTyping');
            if (this.chatTypingUsers.length === 0) {
                el.classList.remove('visible');
                return;
            }

            const names = this.chatTypingUsers.map(u => u.userName);
            if (names.length === 1) {
                el.textContent = names[0] + ' ' + this.t('chatTyping');
            } else {
                el.textContent = names.join(', ') + ' ' + this.t('chatTypingMultiple');
            }
            el.classList.add('visible');
        },

        /**
         * Format chat message time
         */
        formatChatTime: function (timestamp) {
            const date = new Date(timestamp);
            const now = new Date();
            const diff = (now - date) / 1000;

            if (diff < 60) return this.t('chatJustNow');
            if (diff < 3600) return Math.floor(diff / 60) + this.t('chatMinutesAgo');
            if (diff < 86400) return Math.floor(diff / 3600) + this.t('chatHoursAgo');
            if (diff < 172800) return this.t('chatYesterday');

            return date.toLocaleDateString();
        },

        /**
         * Send chat message
         */
        sendChatMessage: function (gifUrl) {
            const self = this;
            const input = document.getElementById('chatInput');
            const content = gifUrl ? '' : input.value.trim();

            if (!content && !gifUrl) return;
            if (content.length > this.chatConfig.maxMessageLength) {
                alert('Message too long. Max ' + this.chatConfig.maxMessageLength + ' characters.');
                return;
            }

            const baseUrl = ApiClient.serverAddress();
            fetch(baseUrl + '/Ratings/Chat/Messages', {
                method: 'POST',
                credentials: 'include',
                headers: self.getChatAuthHeaders(),
                body: JSON.stringify({ content: content, gifUrl: gifUrl || null })
            })
            .then(function (r) {
                if (r.status === 429) {
                    alert(self.t('chatRateLimited'));
                    return null;
                }
                return r.json();
            })
            .then(function (data) {
                if (data) {
                    input.value = '';
                    input.style.height = 'auto';
                    self.loadChatMessages();
                    // Close GIF picker
                    document.getElementById('chatGifPicker').classList.remove('visible');
                }
            })
            .catch(function () {});
        },

        /**
         * Delete a chat message
         */
        deleteChatMessage: function (messageId) {
            const self = this;
            const baseUrl = ApiClient.serverAddress();
            fetch(baseUrl + '/Ratings/Chat/Messages/' + messageId, {
                method: 'DELETE',
                credentials: 'include',
                headers: self.getChatAuthHeaders()
            })
            .then(function () {
                self.loadChatMessages();
            })
            .catch(function () {});
        },

        /**
         * Notify server of typing
         */
        notifyTyping: function () {
            if (!window.ApiClient) return;
            const self = this;
            const baseUrl = ApiClient.serverAddress();
            fetch(baseUrl + '/Ratings/Chat/Typing', {
                method: 'POST',
                credentials: 'include',
                headers: self.getChatAuthHeaders(),
                body: JSON.stringify({ isTyping: true })
            }).catch(function () {});
        },

        /**
         * Load emoji category
         */
        loadEmojiCategory: function (category) {
            const self = this;
            const container = document.getElementById('chatEmojiList');
            const emojis = this.emojiCategories[category] || [];

            container.innerHTML = emojis.map(function (emoji) {
                return '<button class="chat-emoji-item">' + emoji + '</button>';
            }).join('');

            container.querySelectorAll('.chat-emoji-item').forEach(function (btn) {
                btn.onclick = function () {
                    const input = document.getElementById('chatInput');
                    input.value += this.textContent;
                    input.focus();
                    document.getElementById('chatEmojiPicker').classList.remove('visible');
                };
            });
        },

        /**
         * Load trending GIFs from Tenor
         */
        loadTrendingGifs: function () {
            if (!this.chatConfig.tenorApiKey) return;
            const self = this;
            const url = 'https://tenor.googleapis.com/v2/featured?key=' + this.chatConfig.tenorApiKey + '&limit=20&media_filter=gif';

            fetch(url)
                .then(function (r) { return r.json(); })
                .then(function (data) {
                    self.renderGifs(data.results || []);
                })
                .catch(function () {});
        },

        /**
         * Search GIFs from Tenor
         */
        searchGifs: function (query) {
            if (!this.chatConfig.tenorApiKey) return;
            const self = this;
            const url = 'https://tenor.googleapis.com/v2/search?key=' + this.chatConfig.tenorApiKey + '&q=' + encodeURIComponent(query) + '&limit=20&media_filter=gif';

            fetch(url)
                .then(function (r) { return r.json(); })
                .then(function (data) {
                    self.renderGifs(data.results || []);
                })
                .catch(function () {});
        },

        /**
         * Render GIF results
         */
        renderGifs: function (gifs) {
            const self = this;
            const container = document.getElementById('chatGifList');

            container.innerHTML = gifs.map(function (gif) {
                const previewUrl = gif.media_formats.tinygif?.url || gif.media_formats.nanogif?.url || '';
                const fullUrl = gif.media_formats.gif?.url || previewUrl;
                return '<div class="chat-gif-item" data-url="' + fullUrl + '"><img src="' + previewUrl + '" alt="GIF"></div>';
            }).join('');

            container.querySelectorAll('.chat-gif-item').forEach(function (item) {
                item.onclick = function () {
                    self.sendChatMessage(this.dataset.url);
                };
            });
        },

        /**
         * Load admin panel data
         */
        loadAdminPanel: function () {
            this.loadModerators();
            this.loadBannedUsers();
        },

        /**
         * Load moderators list
         */
        loadModerators: function () {
            const self = this;
            const baseUrl = ApiClient.serverAddress();
            fetch(baseUrl + '/Ratings/Chat/Moderators', {
                method: 'GET',
                credentials: 'include',
                headers: self.getChatAuthHeaders()
            })
            .then(function (r) { return r.json(); })
            .then(function (mods) {
                const list = document.getElementById('chatModeratorList');
                if (mods.length === 0) {
                    list.innerHTML = '<div style="color:#666;font-size:12px;">No moderators</div>';
                } else {
                    list.innerHTML = mods.map(function (mod) {
                        return '<div class="chat-admin-item">'
                            + '<span class="chat-admin-user">' + self.escapeHtml(mod.userName) + '</span>'
                            + (self.chatIsAdmin ? '<button class="chat-admin-btn danger" onclick="RatingsPlugin.removeModerator(\'' + mod.userId + '\')">' + self.t('chatRemoveMod') + '</button>' : '')
                            + '</div>';
                    }).join('');
                }
            })
            .catch(function () {});
        },

        /**
         * Load banned users list
         */
        loadBannedUsers: function () {
            const self = this;
            const baseUrl = ApiClient.serverAddress();
            fetch(baseUrl + '/Ratings/Chat/Ban/List', {
                method: 'GET',
                credentials: 'include',
                headers: self.getChatAuthHeaders()
            })
            .then(function (r) { return r.json(); })
            .then(function (bans) {
                const list = document.getElementById('chatBannedList');
                if (!bans || bans.length === 0) {
                    list.innerHTML = '<div style="color:#666;font-size:12px;">' + self.t('noBannedUsers') + '</div>';
                } else {
                    list.innerHTML = bans.map(function (ban) {
                        return '<div class="chat-admin-item">'
                            + '<span class="chat-admin-user">' + self.escapeHtml(ban.userName || 'Unknown') + '</span>'
                            + '<button class="chat-admin-btn" onclick="RatingsPlugin.unbanChatUser(\'' + ban.userId + '\')">' + self.t('chatUnban') + '</button>'
                            + '</div>';
                    }).join('');
                }
            })
            .catch(function () {
                document.getElementById('chatBannedList').innerHTML = '<div style="color:#666;font-size:12px;">' + self.t('noBannedUsers') + '</div>';
            });
        },

        /**
         * Show ban user dialog
         */
        showBanUserDialog: function (userId, userName) {
            const self = this;
            const duration = prompt('Ban duration:\n1 = 10 minutes\n2 = 1 hour\n3 = 1 day\n4 = 1 week\n5 = Permanent', '1');
            if (!duration) return;

            let minutes = 10;
            switch (duration) {
                case '2': minutes = 60; break;
                case '3': minutes = 60 * 24; break;
                case '4': minutes = 60 * 24 * 7; break;
                case '5': minutes = 0; break; // Permanent
            }

            this.banChatUser(userId, 'chat', minutes);
        },

        /**
         * Ban a user from chat
         */
        banChatUser: function (userId, banType, durationMinutes) {
            const self = this;
            const baseUrl = ApiClient.serverAddress();
            fetch(baseUrl + '/Ratings/Chat/Ban', {
                method: 'POST',
                credentials: 'include',
                headers: self.getChatAuthHeaders(),
                body: JSON.stringify({
                    userId: userId,
                    banType: banType,
                    durationMinutes: durationMinutes,
                    reason: 'Banned by moderator'
                })
            })
            .then(function () {
                self.loadBannedUsers();
            })
            .catch(function () {});
        },

        /**
         * Unban a user
         */
        unbanChatUser: function (userId) {
            const self = this;
            const baseUrl = ApiClient.serverAddress();
            fetch(baseUrl + '/Ratings/Chat/Ban?userId=' + userId, {
                method: 'DELETE',
                credentials: 'include',
                headers: self.getChatAuthHeaders()
            })
            .then(function () {
                self.loadBannedUsers();
            })
            .catch(function () {});
        },

        /**
         * Add moderator
         */
        addModerator: function (userId) {
            const self = this;
            const baseUrl = ApiClient.serverAddress();
            fetch(baseUrl + '/Ratings/Chat/Moderators', {
                method: 'POST',
                credentials: 'include',
                headers: self.getChatAuthHeaders(),
                body: JSON.stringify({ userId: userId })
            })
            .then(function () {
                self.loadModerators();
            })
            .catch(function () {});
        },

        /**
         * Remove moderator
         */
        removeModerator: function (userId) {
            const self = this;
            const baseUrl = ApiClient.serverAddress();
            fetch(baseUrl + '/Ratings/Chat/Moderators/' + userId, {
                method: 'DELETE',
                credentials: 'include',
                headers: self.getChatAuthHeaders()
            })
            .then(function () {
                self.loadModerators();
            })
            .catch(function () {});
        },

        /**
         * Clear all chat messages (admin only)
         */
        clearAllChatMessages: function () {
            const self = this;
            const baseUrl = ApiClient.serverAddress();
            fetch(baseUrl + '/Ratings/Chat/Messages/Clear', {
                method: 'DELETE',
                credentials: 'include',
                headers: self.getChatAuthHeaders()
            })
            .then(function () {
                self.chatMessages = [];
                self.renderChatMessages();
                alert(self.t('chatCleared'));
            })
            .catch(function () {});
        },

        /**
         * Update unread badge
         */
        updateUnreadBadge: function (count) {
            const badge = document.getElementById('chatBadge');
            if (badge) {
                if (count > 0) {
                    badge.textContent = count > 99 ? '99+' : count;
                    badge.classList.remove('hidden');
                } else {
                    badge.classList.add('hidden');
                }
            }
            this.chatUnreadCount = count;
        },

        /**
         * Escape HTML for XSS prevention
         */
        escapeHtml: function (text) {
            if (!text) return '';
            const div = document.createElement('div');
            div.textContent = text;
            return div.innerHTML;
        }
    };

    // Initialize when DOM is ready

    function initPlugin() {
        RatingsPlugin.init();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initPlugin);
    } else {
        // Try immediate init
        initPlugin();
    }

    // Also try after a delay to ensure Jellyfin is fully loaded
    setTimeout(initPlugin, 2000);

    // Make it globally available
    window.RatingsPlugin = RatingsPlugin;
})();
