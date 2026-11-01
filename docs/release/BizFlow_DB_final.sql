--
-- PostgreSQL database dump
--

\restrict r5KTmKCRf2LmYe43kXW0CiGwe0H4MkTvtfUUOouuyODogywdj9u1qk7wOnv84lv

-- Dumped from database version 18.3
-- Dumped by pg_dump version 18.3

SET statement_timeout = 0;
SET lock_timeout = 0;
SET idle_in_transaction_session_timeout = 0;
SET transaction_timeout = 0;
SET client_encoding = 'UTF8';
SET standard_conforming_strings = on;
SELECT pg_catalog.set_config('search_path', '', false);
SET check_function_bodies = false;
SET xmloption = content;
SET client_min_messages = warning;
SET row_security = off;

--
-- Name: audit_actor_type; Type: TYPE; Schema: public; Owner: -
--

CREATE TYPE public.audit_actor_type AS ENUM (
    'AI_AGENT',
    'SYSTEM',
    'USER'
);


--
-- Name: company_status; Type: TYPE; Schema: public; Owner: -
--

CREATE TYPE public.company_status AS ENUM (
    'ACTIVE',
    'INACTIVE',
    'PENDING',
    'SUSPENDED'
);


--
-- Name: permission_scope; Type: TYPE; Schema: public; Owner: -
--

CREATE TYPE public.permission_scope AS ENUM (
    'ASSIGNED',
    'DEPARTMENT',
    'PLATFORM',
    'SELF',
    'TENANT'
);


--
-- Name: record_status; Type: TYPE; Schema: public; Owner: -
--

CREATE TYPE public.record_status AS ENUM (
    'ACTIVE',
    'INACTIVE'
);


--
-- Name: request_priority; Type: TYPE; Schema: public; Owner: -
--

CREATE TYPE public.request_priority AS ENUM (
    'CRITICAL',
    'HIGH',
    'LOW',
    'MEDIUM'
);


--
-- Name: request_state; Type: TYPE; Schema: public; Owner: -
--

CREATE TYPE public.request_state AS ENUM (
    'CANCELLED',
    'CLOSED',
    'CONFIRMED',
    'DRAFT',
    'IN_PROGRESS',
    'OVERDUE',
    'RECEIVED',
    'REJECTED',
    'RESOLVED',
    'ROUTED',
    'SUBMITTED',
    'WAITING_FOR_INFORMATION'
);


--
-- Name: service_category_status; Type: TYPE; Schema: public; Owner: -
--

CREATE TYPE public.service_category_status AS ENUM (
    'ACTIVE',
    'INACTIVE'
);


--
-- Name: service_status; Type: TYPE; Schema: public; Owner: -
--

CREATE TYPE public.service_status AS ENUM (
    'ACTIVE',
    'DRAFT',
    'INACTIVE'
);


--
-- Name: sla_profile_status; Type: TYPE; Schema: public; Owner: -
--

CREATE TYPE public.sla_profile_status AS ENUM (
    'ACTIVE',
    'DRAFT',
    'INACTIVE'
);


--
-- Name: task_priority; Type: TYPE; Schema: public; Owner: -
--

CREATE TYPE public.task_priority AS ENUM (
    'CRITICAL',
    'HIGH',
    'LOW',
    'MEDIUM'
);


--
-- Name: task_state; Type: TYPE; Schema: public; Owner: -
--

CREATE TYPE public.task_state AS ENUM (
    'ACCEPTED',
    'ASSIGNED',
    'CANCELLED',
    'COMPLETED',
    'CONFIRMED',
    'DRAFT',
    'IN_PROGRESS',
    'OVERDUE',
    'REJECTED',
    'SUBMITTED'
);


--
-- Name: tenant_status; Type: TYPE; Schema: public; Owner: -
--

CREATE TYPE public.tenant_status AS ENUM (
    'ACTIVE',
    'INACTIVE',
    'PROVISIONING',
    'SUSPENDED'
);


--
-- Name: user_status; Type: TYPE; Schema: public; Owner: -
--

CREATE TYPE public.user_status AS ENUM (
    'ACTIVE',
    'INACTIVE',
    'LOCKED'
);


--
-- Name: workflow_business_type; Type: TYPE; Schema: public; Owner: -
--

CREATE TYPE public.workflow_business_type AS ENUM (
    'REQUEST',
    'TASK'
);


--
-- Name: workflow_status; Type: TYPE; Schema: public; Owner: -
--

CREATE TYPE public.workflow_status AS ENUM (
    'ACTIVE',
    'DRAFT',
    'INACTIVE'
);


--
-- Name: workflow_step_type; Type: TYPE; Schema: public; Owner: -
--

CREATE TYPE public.workflow_step_type AS ENUM (
    'ACTION',
    'APPROVAL',
    'CONFIRMATION'
);


--
-- Name: workflow_version_status; Type: TYPE; Schema: public; Owner: -
--

CREATE TYPE public.workflow_version_status AS ENUM (
    'DRAFT',
    'PUBLISHED',
    'RETIRED'
);


--
-- Name: bizflow_attachment_guard(); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.bizflow_attachment_guard() RETURNS trigger
    LANGUAGE plpgsql
    AS $$
DECLARE
    v_uploader_tenant uuid;
    v_target_tenant uuid;
BEGIN
    -- Check uploader belongs to same tenant
    SELECT "TenantId" INTO v_uploader_tenant FROM "User" WHERE "UserId" = NEW."UploadedBy" AND "DeletedAt" IS NULL;
    IF v_uploader_tenant IS NULL OR v_uploader_tenant <> NEW."TenantId" THEN
        RAISE EXCEPTION 'ATTACHMENT.INVALID_UPLOADER: Uploader must belong to the exact same tenant.' USING ERRCODE = '23503';
    END IF;

    -- Check target exists in same tenant
    IF NEW."ObjectType" = 'TASK' THEN
        SELECT "TenantId" INTO v_target_tenant FROM "Task" WHERE "TaskId" = NEW."ObjectId" AND "DeletedAt" IS NULL;
    ELSIF NEW."ObjectType" = 'REQUEST' THEN
        SELECT "TenantId" INTO v_target_tenant FROM "Request" WHERE "RequestId" = NEW."ObjectId" AND "DeletedAt" IS NULL;
    ELSIF NEW."ObjectType" = 'COMMENT' THEN
        SELECT "TenantId" INTO v_target_tenant FROM "Comment" WHERE "CommentId" = NEW."ObjectId" AND "DeletedAt" IS NULL;
    ELSIF NEW."ObjectType" = 'RESULT' THEN
        SELECT t."TenantId" INTO v_target_tenant FROM "TaskResult" r JOIN "Task" t ON t."TaskId" = r."TaskId" WHERE r."TaskResultId" = NEW."ObjectId" AND t."DeletedAt" IS NULL;
    ELSIF NEW."ObjectType" = 'PROGRESS' THEN
        SELECT t."TenantId" INTO v_target_tenant FROM "TaskProgressReport" p JOIN "Task" t ON t."TaskId" = p."TaskId" WHERE p."ProgressReportId" = NEW."ObjectId" AND t."DeletedAt" IS NULL;
    ELSE
        RAISE EXCEPTION 'ATTACHMENT.INVALID_OBJECT_TYPE: Unsupported object type.' USING ERRCODE = '23514';
    END IF;

    IF v_target_tenant IS NULL OR v_target_tenant <> NEW."TenantId" THEN
        RAISE EXCEPTION 'ATTACHMENT.TARGET_NOT_FOUND: Target object not found in the exact same tenant.' USING ERRCODE = '23503';
    END IF;

    -- Immutability check on UPDATE
    IF TG_OP = 'UPDATE' THEN
        IF NEW."TenantId" <> OLD."TenantId" OR
           NEW."ObjectType" <> OLD."ObjectType" OR
           NEW."ObjectId" <> OLD."ObjectId" OR
           NEW."UploadedBy" <> OLD."UploadedBy" OR
           NEW."ObjectKey" <> OLD."ObjectKey" THEN
            RAISE EXCEPTION 'ATTACHMENT.IMMUTABLE_FIELDS: TenantId, ObjectType, ObjectId, UploadedBy, and ObjectKey cannot be changed.' USING ERRCODE = '23514';
        END IF;
    END IF;

    RETURN NEW;
END;
$$;


--
-- Name: bizflow_audit_immutable(); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.bizflow_audit_immutable() RETURNS trigger
    LANGUAGE plpgsql
    AS $$
BEGIN
    RAISE EXCEPTION 'Audit records are append-only' USING ERRCODE = '23514';
END $$;


--
-- Name: bizflow_calendar_guard(); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.bizflow_calendar_guard() RETURNS trigger
    LANGUAGE plpgsql
    AS $$
BEGIN
    IF TG_OP = 'DELETE' THEN
        IF EXISTS (SELECT 1 FROM "SLAVersion" WHERE "CalendarId"=OLD."CalendarId") THEN
            RAISE EXCEPTION 'Referenced SLA calendars are immutable' USING ERRCODE='23514';
        END IF;
        RETURN OLD;
    END IF;
    IF TG_OP = 'UPDATE' THEN
        IF NEW."CalendarId" IS DISTINCT FROM OLD."CalendarId" OR NEW."TenantId" IS DISTINCT FROM OLD."TenantId" THEN
            RAISE EXCEPTION 'Calendar identity and tenant are immutable' USING ERRCODE='23514';
        END IF;
        -- A value-preserving row touch is used by reference creation solely for MVCC serialization.
        IF NEW IS NOT DISTINCT FROM OLD THEN RETURN NEW; END IF;
        IF EXISTS (SELECT 1 FROM "SLAVersion" WHERE "CalendarId"=OLD."CalendarId") THEN
            RAISE EXCEPTION 'Referenced SLA calendars are immutable' USING ERRCODE='23514';
        END IF;
    END IF;
    IF NOT bizflow_calendar_schema(NEW."WorkingHoursJson", NEW."HolidaysJson") OR
        NOT EXISTS (SELECT 1 FROM pg_timezone_names WHERE name=NEW."TimeZone") THEN
        RAISE EXCEPTION 'Invalid business calendar configuration' USING ERRCODE='23514';
    END IF;
    RETURN NEW;
END $$;


--
-- Name: bizflow_calendar_schema(jsonb, jsonb); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.bizflow_calendar_schema(hours jsonb, holidays jsonb) RETURNS boolean
    LANGUAGE plpgsql IMMUTABLE
    AS $_$
DECLARE day record; slot jsonb; holiday jsonb; start_min integer; end_min integer; previous_end integer;
BEGIN
    IF jsonb_typeof(hours) IS DISTINCT FROM 'object' OR jsonb_typeof(holidays) IS DISTINCT FROM 'array' THEN RETURN false; END IF;
    FOR day IN SELECT key, value FROM jsonb_each(hours) LOOP
        IF day.key NOT IN ('monday','tuesday','wednesday','thursday','friday','saturday','sunday') OR jsonb_typeof(day.value) <> 'array' THEN RETURN false; END IF;
        FOR slot IN SELECT value FROM jsonb_array_elements(day.value) LOOP
            IF jsonb_typeof(slot) <> 'object' THEN RETURN false; END IF;
            IF (SELECT count(*) FROM jsonb_object_keys(slot)) <> 2 OR
                jsonb_typeof(slot->'start') IS DISTINCT FROM 'string' OR jsonb_typeof(slot->'end') IS DISTINCT FROM 'string' OR
                (slot->>'start') !~ '^([01][0-9]|2[0-3]):[0-5][0-9]$' OR
                (slot->>'end') !~ '^(([01][0-9]|2[0-3]):[0-5][0-9]|24:00)$' THEN RETURN false; END IF;
        END LOOP;
        previous_end := -1;
        FOR slot IN SELECT value FROM jsonb_array_elements(day.value) ORDER BY (value->>'start') COLLATE "C" LOOP
            start_min := substring(slot->>'start',1,2)::integer * 60 + substring(slot->>'start',4,2)::integer;
            end_min := substring(slot->>'end',1,2)::integer * 60 + substring(slot->>'end',4,2)::integer;
            IF end_min <= start_min OR start_min < previous_end THEN RETURN false; END IF;
            previous_end := end_min;
        END LOOP;
    END LOOP;
    IF (SELECT count(*) FROM jsonb_array_elements(holidays)) <> (SELECT count(DISTINCT value) FROM jsonb_array_elements(holidays)) THEN RETURN false; END IF;
    FOR holiday IN SELECT value FROM jsonb_array_elements(holidays) LOOP
        IF jsonb_typeof(holiday) <> 'string' OR (holiday#>>'{}') !~ '^[0-9]{4}-[0-9]{2}-[0-9]{2}$' THEN RETURN false; END IF;
        PERFORM make_date(substring(holiday#>>'{}',1,4)::integer, substring(holiday#>>'{}',6,2)::integer, substring(holiday#>>'{}',9,2)::integer);
    END LOOP;
    RETURN true;
EXCEPTION WHEN datetime_field_overflow OR invalid_datetime_format THEN RETURN false;
END $_$;


--
-- Name: bizflow_comment_guard(); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.bizflow_comment_guard() RETURNS trigger
    LANGUAGE plpgsql
    AS $$
DECLARE
    v_author_tenant uuid;
    v_target_tenant uuid;
BEGIN
    -- Validate author belongs to the same tenant
    SELECT "TenantId" INTO v_author_tenant FROM "User" WHERE "UserId" = NEW."AuthorId";
    IF v_author_tenant IS NULL OR v_author_tenant <> NEW."TenantId" THEN
        RAISE EXCEPTION 'Comment author must belong to the comment tenant.' USING ERRCODE = '23503';
    END IF;

    -- Validate target object belongs to the same tenant
    IF NEW."ObjectType" = 'TASK' THEN
        SELECT "TenantId" INTO v_target_tenant FROM "Task" WHERE "TaskId" = NEW."ObjectId";
        IF v_target_tenant IS NULL OR v_target_tenant <> NEW."TenantId" THEN
            RAISE EXCEPTION 'Target Task must exist in the same tenant.' USING ERRCODE = '23503';
        END IF;
    ELSIF NEW."ObjectType" = 'REQUEST' THEN
        SELECT "TenantId" INTO v_target_tenant FROM "Request" WHERE "RequestId" = NEW."ObjectId";
        IF v_target_tenant IS NULL OR v_target_tenant <> NEW."TenantId" THEN
            RAISE EXCEPTION 'Target Request must exist in the same tenant.' USING ERRCODE = '23503';
        END IF;
    END IF;

    -- Immutability on update
    IF TG_OP = 'UPDATE' THEN
        IF OLD."TenantId" <> NEW."TenantId" THEN
            RAISE EXCEPTION 'Comment TenantId is immutable.' USING ERRCODE = '23514';
        END IF;
        IF OLD."ObjectType" <> NEW."ObjectType" THEN
            RAISE EXCEPTION 'Comment ObjectType is immutable.' USING ERRCODE = '23514';
        END IF;
        IF OLD."ObjectId" <> NEW."ObjectId" THEN
            RAISE EXCEPTION 'Comment ObjectId is immutable.' USING ERRCODE = '23514';
        END IF;
        IF OLD."AuthorId" <> NEW."AuthorId" THEN
            RAISE EXCEPTION 'Comment AuthorId is immutable.' USING ERRCODE = '23514';
        END IF;
    END IF;

    RETURN NEW;
END;
$$;


--
-- Name: bizflow_confirmation_guard(); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.bizflow_confirmation_guard() RETURNS trigger
    LANGUAGE plpgsql
    AS $$
BEGIN
  IF TG_OP <> 'INSERT' THEN
    RAISE EXCEPTION 'Confirmation history is immutable' USING ERRCODE='23514';
  END IF;
  IF NEW."ObjectType"='TASK' THEN
    PERFORM 1 FROM "Task" WHERE "TaskId"=NEW."ObjectId" AND "TenantId"=NEW."TenantId" AND "DeletedAt" IS NULL FOR UPDATE;
  ELSE
    PERFORM 1 FROM "Request" WHERE "RequestId"=NEW."ObjectId" AND "TenantId"=NEW."TenantId" AND "DeletedAt" IS NULL FOR UPDATE;
  END IF;
  IF NOT FOUND THEN RAISE EXCEPTION 'Confirmation target must belong to its tenant' USING ERRCODE='23514'; END IF;
  PERFORM 1 FROM "User" WHERE "UserId"=NEW."ActorId" AND "TenantId"=NEW."TenantId" AND "Status"='ACTIVE' AND "DeletedAt" IS NULL FOR SHARE;
  IF NOT FOUND THEN RAISE EXCEPTION 'Confirmation actor must be active in its tenant' USING ERRCODE='23514'; END IF;
  IF NEW."Note" IS NOT NULL AND translate(NEW."Note", E'\r\n\t', '') ~ '[[:cntrl:]]' THEN
    RAISE EXCEPTION 'Confirmation note must be plain text' USING ERRCODE='23514';
  END IF;
  RETURN NEW;
END $$;


--
-- Name: bizflow_escalation_schema(jsonb); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.bizflow_escalation_schema(config jsonb) RETURNS boolean
    LANGUAGE plpgsql IMMUTABLE
    AS $_$
DECLARE item jsonb; expected_level integer := 1; previous_offset integer := -1; offset_minutes integer;
BEGIN
    IF config IS NULL THEN RETURN true; END IF;
    IF jsonb_typeof(config) IS DISTINCT FROM 'object' THEN RETURN false; END IF;
    IF (SELECT count(*) FROM jsonb_object_keys(config)) <> 1 OR jsonb_typeof(config->'levels') IS DISTINCT FROM 'array' THEN RETURN false; END IF;
    FOR item IN SELECT value FROM jsonb_array_elements(config->'levels') LOOP
        IF jsonb_typeof(item) <> 'object' THEN RETURN false; END IF;
        IF (SELECT count(*) FROM jsonb_object_keys(item)) <> 3 OR jsonb_typeof(item->'level') IS DISTINCT FROM 'number' OR
            jsonb_typeof(item->'afterMinutes') IS DISTINCT FROM 'number' OR jsonb_typeof(item->'recipientUserIds') IS DISTINCT FROM 'array' THEN RETURN false; END IF;
        IF (item->>'level') !~ '^[0-9]+$' OR (item->>'afterMinutes') !~ '^[0-9]+$' OR (item->>'level')::integer <> expected_level THEN RETURN false; END IF;
        offset_minutes := (item->>'afterMinutes')::integer;
        IF offset_minutes <= previous_offset OR jsonb_array_length(item->'recipientUserIds') = 0 THEN RETURN false; END IF;
        IF EXISTS (SELECT 1 FROM jsonb_array_elements(item->'recipientUserIds') WHERE jsonb_typeof(value) <> 'string' OR
            (value#>>'{}') !~ '^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$' OR
            (value#>>'{}') = '00000000-0000-0000-0000-000000000000') THEN RETURN false; END IF;
        IF (SELECT count(*) FROM jsonb_array_elements_text(item->'recipientUserIds')) <>
            (SELECT count(DISTINCT lower(value)) FROM jsonb_array_elements_text(item->'recipientUserIds')) THEN RETURN false; END IF;
        expected_level := expected_level + 1; previous_offset := offset_minutes;
    END LOOP;
    RETURN true;
EXCEPTION WHEN numeric_value_out_of_range OR invalid_text_representation THEN RETURN false;
END $_$;


--
-- Name: bizflow_management_scope_boundary(); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.bizflow_management_scope_boundary() RETURNS trigger
    LANGUAGE plpgsql
    AS $$
BEGIN
    IF TG_OP = 'UPDATE' AND (
        NEW."TenantId" IS DISTINCT FROM OLD."TenantId" OR
        NEW."UserId" IS DISTINCT FROM OLD."UserId" OR
        NEW."CreatedBy" IS DISTINCT FROM OLD."CreatedBy" OR
        NEW."CreatedAt" IS DISTINCT FROM OLD."CreatedAt") THEN
        RAISE EXCEPTION 'Management scope ownership and attribution are immutable' USING ERRCODE = '23514';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM "User" WHERE "UserId" = NEW."UserId" AND "TenantId" = NEW."TenantId")
        OR NOT EXISTS (SELECT 1 FROM "User" WHERE "UserId" = NEW."CreatedBy" AND "TenantId" = NEW."TenantId") THEN
        RAISE EXCEPTION 'Management scope user and creator must belong to its tenant' USING ERRCODE = '23514';
    END IF;
    RETURN NEW;
END $$;


--
-- Name: bizflow_notification_boundary(); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.bizflow_notification_boundary() RETURNS trigger
    LANGUAGE plpgsql
    AS $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM "User" WHERE "UserId"=NEW."RecipientId" AND "TenantId"=NEW."TenantId") THEN
        RAISE EXCEPTION 'Notification recipient must belong to its tenant' USING ERRCODE='23514';
    END IF;
    IF TG_OP='UPDATE' THEN
        IF (to_jsonb(NEW) - 'ReadAt' - 'SentAt') IS DISTINCT FROM (to_jsonb(OLD) - 'ReadAt' - 'SentAt') THEN
            RAISE EXCEPTION 'Notification identity and event payload are immutable' USING ERRCODE='23514';
        END IF;
        IF (OLD."ReadAt" IS NOT NULL AND NEW."ReadAt" IS DISTINCT FROM OLD."ReadAt") OR
           (OLD."SentAt" IS NOT NULL AND NEW."SentAt" IS DISTINCT FROM OLD."SentAt") THEN
            RAISE EXCEPTION 'Notification receipt timestamps cannot be overwritten' USING ERRCODE='23514';
        END IF;
    END IF;
    RETURN NEW;
END $$;


--
-- Name: bizflow_request_guard(); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.bizflow_request_guard() RETURNS trigger
    LANGUAGE plpgsql
    AS $$
BEGIN
    IF TG_OP='DELETE' THEN
        RAISE EXCEPTION 'Request history cannot be hard deleted' USING ERRCODE='23514';
    END IF;
    IF TG_OP='UPDATE' THEN
        IF OLD."Status"='REJECTED' THEN
            RAISE EXCEPTION 'Rejected requests are immutable; create a revision' USING ERRCODE='23514';
        END IF;
        IF NEW."RequestId" IS DISTINCT FROM OLD."RequestId" OR NEW."TenantId" IS DISTINCT FROM OLD."TenantId" OR
           NEW."RequesterId" IS DISTINCT FROM OLD."RequesterId" OR NEW."CreatedAt" IS DISTINCT FROM OLD."CreatedAt" OR
           NEW."ParentRequestId" IS DISTINCT FROM OLD."ParentRequestId" OR NEW."RevisedFromRequestId" IS DISTINCT FROM OLD."RevisedFromRequestId" THEN
            RAISE EXCEPTION 'Request identity and historical links are immutable' USING ERRCODE='23514';
        END IF;
        IF (OLD."WorkflowVersionId" IS NOT NULL AND NEW."WorkflowVersionId" IS DISTINCT FROM OLD."WorkflowVersionId") OR
           (OLD."SLAVersionId" IS NOT NULL AND NEW."SLAVersionId" IS DISTINCT FROM OLD."SLAVersionId") THEN
            RAISE EXCEPTION 'Applied request configuration versions are immutable' USING ERRCODE='23514';
        END IF;
    END IF;
    IF NEW."Title" ~ '[[:cntrl:]]' OR NEW."Description" !~ '[^[:space:]]' OR
       translate(NEW."Description", E'\r\n\t', '') ~ '[[:cntrl:]]' THEN
        RAISE EXCEPTION 'Request text contains unsupported control characters' USING ERRCODE='23514';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM "User" WHERE "UserId"=NEW."RequesterId" AND "TenantId"=NEW."TenantId") OR
       NOT EXISTS (SELECT 1 FROM "Service" WHERE "ServiceId"=NEW."ServiceId" AND "TenantId"=NEW."TenantId") OR
       NOT EXISTS (SELECT 1 FROM "ServiceCategory" WHERE "ServiceCategoryId"=NEW."CategoryId" AND "ServiceId"=NEW."ServiceId") THEN
        RAISE EXCEPTION 'Request references must belong to its tenant and service' USING ERRCODE='23514';
    END IF;
    -- A parent must already exist, cannot be self, and cannot later be changed.
    -- Thus parent cycles cannot be inserted or formed by concurrent reparenting.
    IF NEW."ParentRequestId" IS NOT NULL THEN
        PERFORM 1 FROM "Request" WHERE "RequestId"=NEW."ParentRequestId" AND "TenantId"=NEW."TenantId" FOR KEY SHARE;
        IF NOT FOUND OR NEW."ParentRequestId"=NEW."RequestId" THEN
            RAISE EXCEPTION 'Request parent must already exist in its tenant' USING ERRCODE='23514';
        END IF;
    END IF;
    IF NEW."RevisedFromRequestId" IS NOT NULL THEN
        PERFORM 1 FROM "Request" WHERE "RequestId"=NEW."RevisedFromRequestId" AND "TenantId"=NEW."TenantId" AND "Status"='REJECTED' FOR KEY SHARE;
        IF NOT FOUND OR NEW."RevisedFromRequestId"=NEW."RequestId" THEN
            RAISE EXCEPTION 'Revision source must be a rejected request in its tenant' USING ERRCODE='23514';
        END IF;
    END IF;
    IF NEW."WorkflowVersionId" IS NOT NULL AND NOT EXISTS (
        SELECT 1 FROM "WorkflowVersion" v JOIN "Workflow" w ON w."WorkflowId"=v."WorkflowId"
        WHERE v."WorkflowVersionId"=NEW."WorkflowVersionId" AND w."TenantId"=NEW."TenantId" AND w."BusinessType"='REQUEST' AND v."Status"='PUBLISHED') THEN
        RAISE EXCEPTION 'Applied workflow must be a published request workflow in its tenant' USING ERRCODE='23514';
    END IF;
    IF NEW."SLAVersionId" IS NOT NULL AND NOT EXISTS (
        SELECT 1 FROM "SLAVersion" v JOIN "SLAProfile" p ON p."SLAProfileId"=v."SLAProfileId"
        WHERE v."SLAVersionId"=NEW."SLAVersionId" AND p."TenantId"=NEW."TenantId") THEN
        RAISE EXCEPTION 'Applied SLA must belong to the request tenant' USING ERRCODE='23514';
    END IF;
    RETURN NEW;
END $$;


--
-- Name: bizflow_request_resolution_guard(); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.bizflow_request_resolution_guard() RETURNS trigger
    LANGUAGE plpgsql
    AS $$
DECLARE parent_tenant uuid; parent_status request_state; parent_deleted timestamptz;
BEGIN
    IF TG_OP <> 'INSERT' THEN
        RAISE EXCEPTION 'Resolution history is append-only' USING ERRCODE='23514';
    END IF;
    -- Serialize evidence insertion with lifecycle changes; reject stale snapshots.
    SELECT "TenantId", "Status", "DeletedAt" INTO parent_tenant, parent_status, parent_deleted
    FROM "Request" WHERE "RequestId"=NEW."RequestId" FOR UPDATE;
    IF NOT FOUND OR parent_status <> 'IN_PROGRESS' OR parent_deleted IS NOT NULL THEN
        RAISE EXCEPTION 'Resolution requires an in-progress request' USING ERRCODE='23514';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM "User" WHERE "UserId"=NEW."ResolverId" AND "TenantId"=parent_tenant) THEN
        RAISE EXCEPTION 'Resolver must belong to the request tenant' USING ERRCODE='23514';
    END IF;
    IF NEW."Content" !~ '[^[:space:]]' OR translate(NEW."Content", E'\r\n\t', '') ~ '[[:cntrl:]]' THEN
        RAISE EXCEPTION 'Resolution requires plain-text content' USING ERRCODE='23514';
    END IF;
    RETURN NEW;
END $$;


--
-- Name: bizflow_role_permission_scope(); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.bizflow_role_permission_scope() RETURNS trigger
    LANGUAGE plpgsql
    AS $$
BEGIN
    IF EXISTS (SELECT 1 FROM "Permission" WHERE "PermissionId" = NEW."PermissionId" AND "ScopeType" = 'PLATFORM')
        AND (EXISTS (SELECT 1 FROM "Role" WHERE "RoleId" = NEW."RoleId" AND NOT "IsSystem")
            OR EXISTS (SELECT 1 FROM "UserRole" ur JOIN "User" u ON u."UserId" = ur."UserId"
                WHERE ur."RoleId" = NEW."RoleId" AND u."TenantId" IS NOT NULL)) THEN
        RAISE EXCEPTION 'Tenant roles cannot grant platform permissions' USING ERRCODE = '23514';
    END IF;
    RETURN NEW;
END $$;


--
-- Name: bizflow_service_category_guard(); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.bizflow_service_category_guard() RETURNS trigger
    LANGUAGE plpgsql
    AS $$
BEGIN
    IF TG_OP='UPDATE' AND (NEW."ServiceCategoryId" IS DISTINCT FROM OLD."ServiceCategoryId" OR NEW."ServiceId" IS DISTINCT FROM OLD."ServiceId") THEN
        RAISE EXCEPTION 'Category identity and service are immutable' USING ERRCODE='23514';
    END IF;
    IF NEW."Code"<>btrim(NEW."Code") OR NEW."Code" ~ '[[:cntrl:]]' OR NEW."Name" ~ '[[:cntrl:]]' THEN
        RAISE EXCEPTION 'Category metadata must be plain text with a trimmed code' USING ERRCODE='23514';
    END IF;
    RETURN NEW;
END $$;


--
-- Name: bizflow_service_guard(); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.bizflow_service_guard() RETURNS trigger
    LANGUAGE plpgsql
    AS $$
BEGIN
    IF TG_OP='UPDATE' AND (NEW."ServiceId" IS DISTINCT FROM OLD."ServiceId" OR
        NEW."TenantId" IS DISTINCT FROM OLD."TenantId" OR NEW."CreatedAt" IS DISTINCT FROM OLD."CreatedAt") THEN
        RAISE EXCEPTION 'Service identity and tenant are immutable' USING ERRCODE='23514';
    END IF;
    IF NEW."Code"<>btrim(NEW."Code") OR NEW."Code" ~ '[[:cntrl:]]' OR NEW."Name" ~ '[[:cntrl:]]' OR
        translate(coalesce(NEW."Description",''), E'\r\n\t', '') ~ '[[:cntrl:]]' THEN
        RAISE EXCEPTION 'Service metadata must be plain text with a trimmed code' USING ERRCODE='23514';
    END IF;
    IF NEW."ActiveWorkflowVersionId" IS NOT NULL AND NOT EXISTS (
        SELECT 1 FROM "WorkflowVersion" v JOIN "Workflow" w ON w."WorkflowId"=v."WorkflowId"
        WHERE v."WorkflowVersionId"=NEW."ActiveWorkflowVersionId" AND w."TenantId"=NEW."TenantId" AND v."Status"='PUBLISHED') THEN
        RAISE EXCEPTION 'Service workflow must be published in its tenant' USING ERRCODE='23514';
    END IF;
    IF NEW."ActiveSLAVersionId" IS NOT NULL AND NOT EXISTS (
        SELECT 1 FROM "SLAVersion" v JOIN "SLAProfile" p ON p."SLAProfileId"=v."SLAProfileId"
        WHERE v."SLAVersionId"=NEW."ActiveSLAVersionId" AND p."TenantId"=NEW."TenantId") THEN
        RAISE EXCEPTION 'Service SLA must belong to its tenant' USING ERRCODE='23514';
    END IF;
    RETURN NEW;
END $$;


--
-- Name: bizflow_sla_profile_identity(); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.bizflow_sla_profile_identity() RETURNS trigger
    LANGUAGE plpgsql
    AS $$
BEGIN
    IF NEW."TenantId" IS DISTINCT FROM OLD."TenantId" OR NEW."SLAProfileId" IS DISTINCT FROM OLD."SLAProfileId" THEN
        RAISE EXCEPTION 'SLA profile identity and tenant are immutable' USING ERRCODE='23514';
    END IF;
    RETURN NEW;
END $$;


--
-- Name: bizflow_sla_version_immutable(); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.bizflow_sla_version_immutable() RETURNS trigger
    LANGUAGE plpgsql
    AS $$
BEGIN
    RAISE EXCEPTION 'Saved SLA versions are immutable' USING ERRCODE='23514';
END $$;


--
-- Name: bizflow_sla_version_references(); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.bizflow_sla_version_references() RETURNS trigger
    LANGUAGE plpgsql
    AS $$
DECLARE profile_tenant uuid; calendar_tenant uuid; hours jsonb; recipient uuid;
    recipient_tenant uuid; recipient_status text; recipient_deleted timestamptz;
BEGIN
    IF NOT bizflow_escalation_schema(NEW."EscalationConfigJson") THEN
        RAISE EXCEPTION 'Invalid escalation configuration' USING ERRCODE='23514';
    END IF;
    SELECT "TenantId" INTO profile_tenant FROM "SLAProfile" WHERE "SLAProfileId"=NEW."SLAProfileId" FOR KEY SHARE;
    -- A lock alone is insufficient when a calendar editor has an older repeatable-read snapshot.
    -- Touch xmin without changing any calendar values; such an editor must then serialize or abort.
    UPDATE "BusinessCalendar" SET "TimeZone"="TimeZone" WHERE "CalendarId"=NEW."CalendarId"
        RETURNING "TenantId", "WorkingHoursJson" INTO calendar_tenant, hours;
    IF profile_tenant IS NULL OR calendar_tenant IS NULL OR profile_tenant <> calendar_tenant THEN
        RAISE EXCEPTION 'SLA profile and calendar must belong to the same tenant' USING ERRCODE='23514';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM jsonb_each(hours) WHERE jsonb_array_length(value) > 0) THEN
        RAISE EXCEPTION 'An SLA requires a calendar with working time' USING ERRCODE='23514';
    END IF;
    FOR recipient IN SELECT DISTINCT target.value::uuid FROM jsonb_array_elements(NEW."EscalationConfigJson"->'levels') AS level(value)
        CROSS JOIN LATERAL jsonb_array_elements_text(level.value->'recipientUserIds') AS target(value) ORDER BY 1 LOOP
        SELECT "TenantId", "Status"::text, "DeletedAt" INTO recipient_tenant, recipient_status, recipient_deleted
            FROM "User" WHERE "UserId"=recipient FOR SHARE;
        IF NOT FOUND OR recipient_tenant IS DISTINCT FROM profile_tenant OR recipient_status <> 'ACTIVE' OR recipient_deleted IS NOT NULL THEN
            RAISE EXCEPTION 'Escalation recipients must be active users in the same tenant' USING ERRCODE='23514';
        END IF;
    END LOOP;
    RETURN NEW;
END $$;


--
-- Name: bizflow_task_assignment_guard(); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.bizflow_task_assignment_guard() RETURNS trigger
    LANGUAGE plpgsql
    AS $$
DECLARE parent_tenant uuid; parent_status task_state; parent_deleted timestamptz; department_claim boolean := false;
BEGIN
    IF TG_OP IN ('DELETE','TRUNCATE') THEN
        RAISE EXCEPTION 'Task assignment history cannot be deleted' USING ERRCODE='23514';
    END IF;
    IF TG_OP='UPDATE' THEN
        department_claim := OLD."UserId" IS NULL AND NEW."UserId" IS NOT NULL AND OLD."DepartmentId" IS NOT NULL AND
            OLD."AcceptedAt" IS NULL AND OLD."RejectedAt" IS NULL AND OLD."EndedAt" IS NULL AND
            NEW."AcceptedAt" IS NOT NULL AND NEW."RejectedAt" IS NULL AND NEW."EndedAt" IS NULL;
        IF OLD."EndedAt" IS NOT NULL THEN
            RAISE EXCEPTION 'Ended assignments are immutable' USING ERRCODE='23514';
        END IF;
        IF NEW."TaskAssignmentId" IS DISTINCT FROM OLD."TaskAssignmentId" OR NEW."TaskId" IS DISTINCT FROM OLD."TaskId" OR
           NEW."DepartmentId" IS DISTINCT FROM OLD."DepartmentId" OR (NEW."UserId" IS DISTINCT FROM OLD."UserId" AND NOT department_claim) OR
           NEW."AssignedBy" IS DISTINCT FROM OLD."AssignedBy" OR NEW."AssignedAt" IS DISTINCT FROM OLD."AssignedAt" THEN
            RAISE EXCEPTION 'Original assignment metadata is immutable' USING ERRCODE='23514';
        END IF;
        IF (OLD."AcceptedAt" IS NOT NULL AND NEW."AcceptedAt" IS DISTINCT FROM OLD."AcceptedAt") OR
           (OLD."RejectedAt" IS NOT NULL AND (NEW."RejectedAt" IS DISTINCT FROM OLD."RejectedAt" OR NEW."RejectionReason" IS DISTINCT FROM OLD."RejectionReason")) THEN
            RAISE EXCEPTION 'Assignment receipt decisions cannot be overwritten' USING ERRCODE='23514';
        END IF;
    END IF;
    SELECT "TenantId", "Status", "DeletedAt" INTO parent_tenant, parent_status, parent_deleted
    FROM "Task" WHERE "TaskId"=NEW."TaskId" FOR UPDATE;
    IF NOT FOUND OR parent_deleted IS NOT NULL THEN
        RAISE EXCEPTION 'Assignment requires a live task' USING ERRCODE='23514';
    END IF;
    IF TG_OP='INSERT' THEN
        IF parent_status IN ('COMPLETED','CANCELLED') OR NEW."AcceptedAt" IS NOT NULL OR NEW."RejectedAt" IS NOT NULL OR
           NEW."RejectionReason" IS NOT NULL OR NEW."EndedAt" IS NOT NULL THEN
            RAISE EXCEPTION 'New assignment must be undecided on a nonterminal task' USING ERRCODE='23514';
        END IF;
        IF NOT EXISTS (SELECT 1 FROM "User" WHERE "UserId"=NEW."AssignedBy" AND "TenantId"=parent_tenant) THEN
            RAISE EXCEPTION 'Assignment actor must belong to the task tenant' USING ERRCODE='23514';
        END IF;
        IF NEW."UserId" IS NOT NULL THEN
            PERFORM 1 FROM "User" WHERE "UserId"=NEW."UserId" AND "TenantId"=parent_tenant AND "Status"='ACTIVE' AND "DeletedAt" IS NULL FOR SHARE;
            IF NOT FOUND THEN
                RAISE EXCEPTION 'Assignment user target must be active in the task tenant' USING ERRCODE='23514';
            END IF;
        END IF;
        IF NEW."DepartmentId" IS NOT NULL THEN
            PERFORM 1 FROM "Department" WHERE "DepartmentId"=NEW."DepartmentId" AND "TenantId"=parent_tenant AND "Status"='ACTIVE' FOR SHARE;
            IF NOT FOUND THEN
                RAISE EXCEPTION 'Assignment department target must be active in the task tenant' USING ERRCODE='23514';
            END IF;
        END IF;
    ELSE
        IF ((OLD."AcceptedAt" IS NULL AND NEW."AcceptedAt" IS NOT NULL) OR (OLD."RejectedAt" IS NULL AND NEW."RejectedAt" IS NOT NULL)) AND parent_status <> 'ASSIGNED' THEN
            RAISE EXCEPTION 'Assignment receipt requires assigned task state' USING ERRCODE='23514';
        END IF;
        IF OLD."AcceptedAt" IS NULL AND NEW."AcceptedAt" IS NOT NULL THEN
            PERFORM 1 FROM "User" WHERE "UserId"=NEW."UserId" AND "TenantId"=parent_tenant AND "Status"='ACTIVE' AND "DeletedAt" IS NULL AND
                (NOT department_claim OR "DepartmentId"=OLD."DepartmentId") FOR SHARE;
            IF NOT FOUND THEN
                RAISE EXCEPTION 'Acceptance requires its active user target or a one-time department-member claim' USING ERRCODE='23514';
            END IF;
        END IF;
    END IF;
    IF NEW."RejectionReason" IS NOT NULL AND (NEW."RejectionReason" !~ '[^[:space:]]' OR translate(NEW."RejectionReason", E'\r\n\t', '') ~ '[[:cntrl:]]') THEN
        RAISE EXCEPTION 'Rejection reason must be nonblank plain text' USING ERRCODE='23514';
    END IF;
    RETURN NEW;
END $$;


--
-- Name: bizflow_task_checklist_guard(); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.bizflow_task_checklist_guard() RETURNS trigger
    LANGUAGE plpgsql
    AS $$
DECLARE parent_tenant uuid; parent_status task_state; parent_deleted timestamptz;
BEGIN
    IF TG_OP IN ('DELETE','TRUNCATE') THEN
        RAISE EXCEPTION 'Task checklist history cannot be hard deleted' USING ERRCODE='23514';
    END IF;
    IF TG_OP='UPDATE' AND (NEW."ChecklistItemId" IS DISTINCT FROM OLD."ChecklistItemId" OR NEW."TaskId" IS DISTINCT FROM OLD."TaskId") THEN
        RAISE EXCEPTION 'Checklist identity and task ownership are immutable' USING ERRCODE='23514';
    END IF;
    SELECT "TenantId", "Status", "DeletedAt" INTO parent_tenant, parent_status, parent_deleted
    FROM "Task" WHERE "TaskId"=NEW."TaskId" FOR UPDATE;
    IF NOT FOUND OR parent_deleted IS NOT NULL OR (TG_OP='INSERT' AND parent_status <> 'DRAFT') THEN
        RAISE EXCEPTION 'Initial checklist requires a live draft task' USING ERRCODE='23514';
    END IF;
    IF NEW."Title" ~ '[[:cntrl:]]' THEN
        RAISE EXCEPTION 'Checklist text contains unsupported control characters' USING ERRCODE='23514';
    END IF;
    IF NEW."CompletedBy" IS NOT NULL AND NOT EXISTS (
        SELECT 1 FROM "User" WHERE "UserId"=NEW."CompletedBy" AND "TenantId"=parent_tenant) THEN
        RAISE EXCEPTION 'Checklist completer must belong to the task tenant' USING ERRCODE='23514';
    END IF;
    RETURN NEW;
END $$;


--
-- Name: bizflow_task_evidence_guard(); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.bizflow_task_evidence_guard() RETURNS trigger
    LANGUAGE plpgsql
    AS $$
DECLARE parent_tenant uuid; parent_status task_state; parent_deleted timestamptz; parent_xmin xid;
BEGIN
    IF TG_OP <> 'INSERT' THEN
        RAISE EXCEPTION 'Task evidence is append-only' USING ERRCODE='23514';
    END IF;
    SELECT "TenantId", "Status", "DeletedAt", xmin INTO parent_tenant, parent_status, parent_deleted, parent_xmin
    FROM "Task" WHERE "TaskId"=NEW."TaskId" FOR UPDATE;
    IF NOT FOUND OR parent_deleted IS NOT NULL THEN
        RAISE EXCEPTION 'Evidence requires a live task' USING ERRCODE='23514';
    END IF;
    -- ADR-0003: overdue work must resume before result submission.
    IF TG_TABLE_NAME='TaskResult' AND parent_status <> 'IN_PROGRESS' THEN
        IF parent_status <> 'SUBMITTED' OR parent_xmin::text <> pg_current_xact_id()::text::xid::text THEN
            RAISE EXCEPTION 'Result submission requires in-progress work' USING ERRCODE='23514';
        END IF;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM "User" WHERE "UserId"=NEW."AuthorId" AND "TenantId"=parent_tenant) THEN
        RAISE EXCEPTION 'Evidence author must belong to the task tenant' USING ERRCODE='23514';
    END IF;
    IF translate(NEW."Content", E'\r\n\t', '') ~ '[[:cntrl:]]' THEN
        RAISE EXCEPTION 'Evidence text contains unsupported control characters' USING ERRCODE='23514';
    END IF;
    RETURN NEW;
END $$;


--
-- Name: bizflow_task_guard(); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.bizflow_task_guard() RETURNS trigger
    LANGUAGE plpgsql
    AS $$
BEGIN
    IF TG_OP IN ('DELETE','TRUNCATE') THEN
        RAISE EXCEPTION 'Task history cannot be hard deleted' USING ERRCODE='23514';
    END IF;
    IF TG_OP='UPDATE' THEN
        IF NEW."TaskId" IS DISTINCT FROM OLD."TaskId" OR NEW."TenantId" IS DISTINCT FROM OLD."TenantId" OR
           NEW."CreatorId" IS DISTINCT FROM OLD."CreatorId" OR NEW."RequestId" IS DISTINCT FROM OLD."RequestId" OR
           NEW."CreatedAt" IS DISTINCT FROM OLD."CreatedAt" THEN
            RAISE EXCEPTION 'Task identity and request traceability are immutable' USING ERRCODE='23514';
        END IF;
        IF (OLD."WorkflowVersionId" IS NOT NULL AND NEW."WorkflowVersionId" IS DISTINCT FROM OLD."WorkflowVersionId") OR
           (OLD."SLAVersionId" IS NOT NULL AND NEW."SLAVersionId" IS DISTINCT FROM OLD."SLAVersionId") THEN
            RAISE EXCEPTION 'Applied task configuration versions are immutable' USING ERRCODE='23514';
        END IF;
    END IF;
    IF NEW."Title" ~ '[[:cntrl:]]' OR translate(NEW."Description", E'\r\n\t', '') ~ '[[:cntrl:]]' THEN
        RAISE EXCEPTION 'Task text contains unsupported control characters' USING ERRCODE='23514';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM "User" WHERE "UserId"=NEW."CreatorId" AND "TenantId"=NEW."TenantId") THEN
        RAISE EXCEPTION 'Task creator must belong to its tenant' USING ERRCODE='23514';
    END IF;
    IF NEW."RequestId" IS NOT NULL AND NOT EXISTS (
        SELECT 1 FROM "Request" WHERE "RequestId"=NEW."RequestId" AND "TenantId"=NEW."TenantId") THEN
        RAISE EXCEPTION 'Task source request must belong to its tenant' USING ERRCODE='23514';
    END IF;
    IF NEW."WorkflowVersionId" IS NOT NULL AND NOT EXISTS (
        SELECT 1 FROM "WorkflowVersion" v JOIN "Workflow" w ON w."WorkflowId"=v."WorkflowId"
        WHERE v."WorkflowVersionId"=NEW."WorkflowVersionId" AND w."TenantId"=NEW."TenantId" AND w."BusinessType"='TASK' AND v."Status"='PUBLISHED') THEN
        RAISE EXCEPTION 'Applied workflow must be a published task workflow in its tenant' USING ERRCODE='23514';
    END IF;
    IF NEW."SLAVersionId" IS NOT NULL AND NOT EXISTS (
        SELECT 1 FROM "SLAVersion" v JOIN "SLAProfile" p ON p."SLAProfileId"=v."SLAProfileId"
        WHERE v."SLAVersionId"=NEW."SLAVersionId" AND p."TenantId"=NEW."TenantId") THEN
        RAISE EXCEPTION 'Applied SLA must belong to the task tenant' USING ERRCODE='23514';
    END IF;
    RETURN NEW;
END $$;


--
-- Name: bizflow_tenant_immutable(); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.bizflow_tenant_immutable() RETURNS trigger
    LANGUAGE plpgsql
    AS $$
BEGIN
    IF NEW."TenantId" IS DISTINCT FROM OLD."TenantId" THEN
        RAISE EXCEPTION 'Tenant ownership is immutable' USING ERRCODE = '23514';
    END IF;
    RETURN NEW;
END $$;


--
-- Name: bizflow_tenant_setting_boundary(); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.bizflow_tenant_setting_boundary() RETURNS trigger
    LANGUAGE plpgsql
    AS $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM "User" WHERE "UserId"=NEW."UpdatedBy" AND
        ("TenantId"=NEW."TenantId" OR ("TenantId" IS NULL AND "IsPlatformAdministrator"))) THEN
        RAISE EXCEPTION 'Setting editor must belong to the tenant or platform plane' USING ERRCODE='23514';
    END IF;
    IF TG_OP='UPDATE' AND (NEW."TenantSettingId" IS DISTINCT FROM OLD."TenantSettingId" OR
        NEW."TenantId" IS DISTINCT FROM OLD."TenantId" OR NEW."Key" IS DISTINCT FROM OLD."Key") THEN
        RAISE EXCEPTION 'Setting ownership and key are immutable' USING ERRCODE='23514';
    END IF;
    RETURN NEW;
END $$;


--
-- Name: bizflow_user_role_scope(); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.bizflow_user_role_scope() RETURNS trigger
    LANGUAGE plpgsql
    AS $$
DECLARE user_tenant uuid; role_tenant uuid;
BEGIN
    SELECT "TenantId" INTO user_tenant FROM "User" WHERE "UserId" = NEW."UserId";
    SELECT "TenantId" INTO role_tenant FROM "Role" WHERE "RoleId" = NEW."RoleId";
    IF role_tenant IS NOT NULL AND role_tenant IS DISTINCT FROM user_tenant THEN
        RAISE EXCEPTION 'Cross-tenant role assignment is forbidden' USING ERRCODE = '23514';
    END IF;
    IF user_tenant IS NOT NULL AND EXISTS (
        SELECT 1 FROM "RolePermission" rp JOIN "Permission" p ON p."PermissionId" = rp."PermissionId"
        WHERE rp."RoleId" = NEW."RoleId" AND p."ScopeType" = 'PLATFORM') THEN
        RAISE EXCEPTION 'Platform permissions cannot be assigned to tenant accounts' USING ERRCODE = '23514';
    END IF;
    RETURN NEW;
END $$;


--
-- Name: bizflow_valid_tenant_setting(text, jsonb); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.bizflow_valid_tenant_setting(policy_key text, policy_value jsonb) RETURNS boolean
    LANGUAGE sql IMMUTABLE
    AS $$
    SELECT CASE
        WHEN policy_key IN ('ai.enabled','ai.auto_action_enabled','sla.pause_on_waiting_for_information',
            'request.generic_service_allowed','notification.email_enabled') THEN jsonb_typeof(policy_value)='boolean'
        WHEN policy_key IN ('ai.auto_action_tools','attachment.allowed_content_types') THEN
            CASE WHEN jsonb_typeof(policy_value)='array' THEN NOT EXISTS
                (SELECT 1 FROM jsonb_array_elements(policy_value) AS item(value)
                 WHERE jsonb_typeof(value)<>'string' OR btrim(value #>> '{}', chr(32)||chr(9)||chr(13)||chr(10))='') ELSE false END
        WHEN policy_key IN ('ai.confidence_threshold','ai.monthly_call_limit','attachment.max_size_bytes','report.max_range_days') THEN
            CASE WHEN jsonb_typeof(policy_value)='number' THEN
                CASE policy_key
                    WHEN 'ai.confidence_threshold' THEN (policy_value #>> '{}')::numeric BETWEEN 0 AND 1
                    WHEN 'attachment.max_size_bytes' THEN (policy_value #>> '{}')::numeric BETWEEN 0 AND 524288000
                        AND trunc((policy_value #>> '{}')::numeric)=(policy_value #>> '{}')::numeric
                    WHEN 'report.max_range_days' THEN (policy_value #>> '{}')::numeric BETWEEN 1 AND 2147483647
                        AND trunc((policy_value #>> '{}')::numeric)=(policy_value #>> '{}')::numeric
                    ELSE (policy_value #>> '{}')::numeric BETWEEN 0 AND 2147483647
                        AND trunc((policy_value #>> '{}')::numeric)=(policy_value #>> '{}')::numeric
                END ELSE false END
        ELSE false
    END
$$;


--
-- Name: bizflow_workflow_child_immutable(); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.bizflow_workflow_child_immutable() RETURNS trigger
    LANGUAGE plpgsql
    AS $$
DECLARE parent_id uuid; parent_status workflow_version_status;
BEGIN
    IF TG_OP = 'UPDATE' AND NEW."WorkflowVersionId" IS DISTINCT FROM OLD."WorkflowVersionId" THEN
        RAISE EXCEPTION 'Workflow child ownership is immutable' USING ERRCODE = '23514';
    END IF;
    IF TG_OP = 'DELETE' THEN parent_id := OLD."WorkflowVersionId";
    ELSE parent_id := NEW."WorkflowVersionId"; END IF;
    -- Same parent lock as publication: waiting writers must see the committed status.
    SELECT "Status" INTO parent_status FROM "WorkflowVersion" WHERE "WorkflowVersionId" = parent_id FOR UPDATE;
    IF parent_status IS NULL OR parent_status <> 'DRAFT' THEN
        RAISE EXCEPTION 'Only draft workflow versions accept child mutations' USING ERRCODE = '23514';
    END IF;
    IF TG_OP = 'DELETE' THEN RETURN OLD; END IF;
    RETURN NEW;
END $$;


--
-- Name: bizflow_workflow_identity(); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.bizflow_workflow_identity() RETURNS trigger
    LANGUAGE plpgsql
    AS $$
BEGIN
    IF NEW."TenantId" IS DISTINCT FROM OLD."TenantId" OR NEW."BusinessType" IS DISTINCT FROM OLD."BusinessType" THEN
        RAISE EXCEPTION 'Workflow tenant and business type are immutable' USING ERRCODE = '23514';
    END IF;
    RETURN NEW;
END $$;


--
-- Name: bizflow_workflow_no_truncate(); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.bizflow_workflow_no_truncate() RETURNS trigger
    LANGUAGE plpgsql
    AS $$
BEGIN RAISE EXCEPTION 'Workflow history cannot be truncated' USING ERRCODE = '23514'; END $$;


--
-- Name: bizflow_workflow_version_immutable(); Type: FUNCTION; Schema: public; Owner: -
--

CREATE FUNCTION public.bizflow_workflow_version_immutable() RETURNS trigger
    LANGUAGE plpgsql
    AS $$
BEGIN
    IF OLD."Status" <> 'DRAFT' THEN
        RAISE EXCEPTION 'Published workflow versions are immutable' USING ERRCODE = '23514';
    END IF;
    IF TG_OP = 'DELETE' THEN RETURN OLD; END IF;
    IF NEW."WorkflowId" IS DISTINCT FROM OLD."WorkflowId" OR NEW."VersionNo" IS DISTINCT FROM OLD."VersionNo" THEN
        RAISE EXCEPTION 'Workflow version identity is immutable' USING ERRCODE = '23514';
    END IF;
    RETURN NEW;
END $$;


SET default_tablespace = '';

SET default_table_access_method = heap;

--
-- Name: AIAgentAction; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."AIAgentAction" (
    "AIAgentActionId" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "AIInteractionId" uuid NOT NULL,
    "ToolName" character varying(100) NOT NULL,
    "ArgsJson" jsonb NOT NULL,
    "AuthorizationResult" character varying(32) NOT NULL,
    "ExecutionStatus" character varying(32) NOT NULL,
    "ResultRef" character varying(255),
    "CreatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "CK_AIAgentAction_AuthorizationResult" CHECK ((("AuthorizationResult")::text = ANY ((ARRAY['AUTHORIZED'::character varying, 'DENIED'::character varying])::text[]))),
    CONSTRAINT "CK_AIAgentAction_ExecutionStatus" CHECK ((("ExecutionStatus")::text = ANY ((ARRAY['NOT_EXECUTED'::character varying, 'SUCCEEDED'::character varying, 'FAILED'::character varying])::text[])))
);


--
-- Name: AIInteraction; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."AIInteraction" (
    "AIInteractionId" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "Feature" character varying(80) NOT NULL,
    "ModelName" character varying(120) NOT NULL,
    "InputRef" character varying(255),
    "OutputRef" character varying(255),
    "Status" character varying(32) NOT NULL,
    "LatencyMs" integer,
    "TokenUsage" jsonb,
    "CreatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "CK_AIInteraction_Status" CHECK ((("Status")::text = ANY ((ARRAY['SUCCEEDED'::character varying, 'FAILED'::character varying, 'TIMEOUT'::character varying, 'REJECTED'::character varying])::text[])))
);


--
-- Name: AIRecommendation; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."AIRecommendation" (
    "RecommendationId" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "AIInteractionId" uuid NOT NULL,
    "ObjectType" character varying(32) NOT NULL,
    "ObjectId" uuid,
    "RecommendationType" character varying(80) NOT NULL,
    "PayloadJson" jsonb NOT NULL,
    "Confidence" numeric(5,4),
    "HumanDecision" character varying(32),
    "DecisionBy" uuid,
    "DecidedAt" timestamp with time zone,
    CONSTRAINT "CK_AIRecommendation_HumanDecision" CHECK ((("HumanDecision" IS NULL) OR (("HumanDecision")::text = ANY ((ARRAY['ACCEPTED'::character varying, 'OVERRIDDEN'::character varying, 'DISMISSED'::character varying])::text[])))),
    CONSTRAINT "CK_AIRecommendation_ObjectType" CHECK ((("ObjectType")::text = ANY ((ARRAY['TASK'::character varying, 'REQUEST'::character varying])::text[])))
);


--
-- Name: AIVectorMemory; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."AIVectorMemory" (
    "MemoryId" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "MemoryScope" character varying(64) NOT NULL,
    "Title" character varying(255) NOT NULL,
    "Content" text NOT NULL,
    "MetadataJson" jsonb,
    "EmbeddingJson" jsonb NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone
);


--
-- Name: Attachment; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."Attachment" (
    "AttachmentId" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "ObjectType" character varying(32) NOT NULL,
    "ObjectId" uuid NOT NULL,
    "UploadedBy" uuid NOT NULL,
    "FileName" character varying(255) NOT NULL,
    "ContentType" character varying(100) NOT NULL,
    "SizeBytes" bigint NOT NULL,
    "ObjectKey" character varying(500) NOT NULL,
    "Hash" character varying(128) NOT NULL,
    "Status" character varying(32) NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "DeletedAt" timestamp with time zone,
    CONSTRAINT "CK_Attachment_ObjectType" CHECK ((("ObjectType")::text = ANY ((ARRAY['TASK'::character varying, 'REQUEST'::character varying, 'COMMENT'::character varying, 'RESULT'::character varying, 'PROGRESS'::character varying])::text[]))),
    CONSTRAINT "CK_Attachment_SizeBytes" CHECK ((("SizeBytes" > 0) AND ("SizeBytes" <= 524288000))),
    CONSTRAINT "CK_Attachment_Status" CHECK ((("Status")::text = ANY ((ARRAY['UPLOADING'::character varying, 'READY'::character varying, 'FAILED'::character varying, 'DELETED'::character varying])::text[])))
);


--
-- Name: AuditLog; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."AuditLog" (
    "AuditLogId" uuid NOT NULL,
    "TenantId" uuid,
    "ActorType" public.audit_actor_type NOT NULL,
    "ActorId" uuid,
    "Action" character varying(100) NOT NULL,
    "ObjectType" character varying(60),
    "ObjectId" uuid,
    "BeforeJson" jsonb,
    "AfterJson" jsonb,
    "MetadataJson" jsonb,
    "CreatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "CK_AuditLog_RequestReplay" CHECK (((("Action")::text <> ALL ((ARRAY['REQUEST.CREATED'::character varying, 'REQUEST.SUBMITTED'::character varying, 'REQUEST.ROUTED'::character varying, 'REQUEST.RECEIVED'::character varying, 'REQUEST.STARTED'::character varying, 'REQUEST.REJECTED'::character varying, 'REQUEST.CANCELLED'::character varying, 'REQUEST.RESOLVED'::character varying, 'REQUEST.CONFIRMED'::character varying, 'REQUEST.REWORK'::character varying, 'REQUEST.REVISED'::character varying])::text[])) OR (NOT COALESCE(("MetadataJson" ? 'idempotency'::text), false)) OR (("TenantId" IS NOT NULL) AND ("ActorId" IS NOT NULL) AND COALESCE(((("MetadataJson" -> 'idempotency'::text) ->> 'operation'::text) = ANY (ARRAY['REQUEST.CREATE'::text, 'REQUEST.SUBMIT'::text, 'REQUEST.ROUTE'::text, 'REQUEST.RECEIVE'::text, 'REQUEST.START'::text, 'REQUEST.REJECT'::text, 'REQUEST.CANCEL'::text, 'REQUEST.RESOLVE'::text, 'REQUEST.CONFIRM'::text, 'REQUEST.REWORK'::text, 'REQUEST.REVISE'::text])), false) AND COALESCE(((("MetadataJson" -> 'idempotency'::text) ->> 'keyHash'::text) ~ '^[0-9a-f]{64}$'::text), false) AND COALESCE(((("MetadataJson" -> 'idempotency'::text) ->> 'fingerprint'::text) ~ '^[0-9a-f]{64}$'::text), false)))),
    CONSTRAINT "CK_AuditLog_TaskAcceptanceReplay" CHECK (((("Action")::text <> 'TASK.ACCEPTED'::text) OR (NOT COALESCE(("MetadataJson" ? 'idempotency'::text), false)) OR (("TenantId" IS NOT NULL) AND ("ActorId" IS NOT NULL) AND COALESCE(((("MetadataJson" -> 'idempotency'::text) ->> 'operation'::text) = 'TASK.ACCEPT'::text), false) AND COALESCE(((("MetadataJson" -> 'idempotency'::text) ->> 'keyHash'::text) ~ '^[0-9a-f]{64}$'::text), false) AND COALESCE(((("MetadataJson" -> 'idempotency'::text) ->> 'fingerprint'::text) ~ '^[0-9a-f]{64}$'::text), false)))),
    CONSTRAINT "CK_AuditLog_TaskAssignmentReplay" CHECK (((("Action")::text <> 'TASK.ASSIGNED'::text) OR (NOT COALESCE(("MetadataJson" ? 'idempotency'::text), false)) OR (("TenantId" IS NOT NULL) AND ("ActorId" IS NOT NULL) AND COALESCE(((("MetadataJson" -> 'idempotency'::text) ->> 'operation'::text) = 'TASK.ASSIGN'::text), false) AND COALESCE(((("MetadataJson" -> 'idempotency'::text) ->> 'keyHash'::text) ~ '^[0-9a-f]{64}$'::text), false) AND COALESCE(((("MetadataJson" -> 'idempotency'::text) ->> 'fingerprint'::text) ~ '^[0-9a-f]{64}$'::text), false)))),
    CONSTRAINT "CK_AuditLog_TaskExecutionReplay" CHECK (((("Action")::text <> 'TASK.STARTED'::text) OR (NOT COALESCE(("MetadataJson" ? 'idempotency'::text), false)) OR (("TenantId" IS NOT NULL) AND ("ActorId" IS NOT NULL) AND COALESCE(((("MetadataJson" -> 'idempotency'::text) ->> 'operation'::text) = 'TASK.START'::text), false) AND COALESCE(((("MetadataJson" -> 'idempotency'::text) ->> 'keyHash'::text) ~ '^[0-9a-f]{64}$'::text), false) AND COALESCE(((("MetadataJson" -> 'idempotency'::text) ->> 'fingerprint'::text) ~ '^[0-9a-f]{64}$'::text), false)))),
    CONSTRAINT "CK_AuditLog_TaskReplay" CHECK (((("Action")::text <> 'TASK.CREATED'::text) OR (NOT COALESCE(("MetadataJson" ? 'idempotency'::text), false)) OR (("TenantId" IS NOT NULL) AND ("ActorId" IS NOT NULL) AND COALESCE(((("MetadataJson" -> 'idempotency'::text) ->> 'operation'::text) = 'TASK.CREATE'::text), false) AND COALESCE(((("MetadataJson" -> 'idempotency'::text) ->> 'keyHash'::text) ~ '^[0-9a-f]{64}$'::text), false) AND COALESCE(((("MetadataJson" -> 'idempotency'::text) ->> 'fingerprint'::text) ~ '^[0-9a-f]{64}$'::text), false)))),
    CONSTRAINT "CK_AuditLog_TaskResultConfirmationReplay" CHECK (((("Action")::text <> ALL ((ARRAY['TASK.RESULT_CONFIRMED'::character varying, 'TASK.RESULT_REWORK'::character varying])::text[])) OR (NOT COALESCE(("MetadataJson" ? 'idempotency'::text), false)) OR (("TenantId" IS NOT NULL) AND ("ActorId" IS NOT NULL) AND COALESCE(((("MetadataJson" -> 'idempotency'::text) ->> 'operation'::text) = ANY (ARRAY['TASK.RESULT_CONFIRM'::text, 'TASK.RESULT_REWORK'::text])), false) AND COALESCE(((("MetadataJson" -> 'idempotency'::text) ->> 'keyHash'::text) ~ '^[0-9a-f]{64}$'::text), false) AND COALESCE(((("MetadataJson" -> 'idempotency'::text) ->> 'fingerprint'::text) ~ '^[0-9a-f]{64}$'::text), false)))),
    CONSTRAINT "CK_AuditLog_TaskResultSubmissionReplay" CHECK (((("Action")::text <> 'TASK.RESULT_SUBMITTED'::text) OR (NOT COALESCE(("MetadataJson" ? 'idempotency'::text), false)) OR (("TenantId" IS NOT NULL) AND ("ActorId" IS NOT NULL) AND COALESCE(((("MetadataJson" -> 'idempotency'::text) ->> 'operation'::text) = 'TASK.RESULT_SUBMIT'::text), false) AND COALESCE(((("MetadataJson" -> 'idempotency'::text) ->> 'keyHash'::text) ~ '^[0-9a-f]{64}$'::text), false) AND COALESCE(((("MetadataJson" -> 'idempotency'::text) ->> 'fingerprint'::text) ~ '^[0-9a-f]{64}$'::text), false))))
);


--
-- Name: AuthenticationSession; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."AuthenticationSession" (
    "AuthenticationSessionId" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "FamilyId" uuid NOT NULL,
    "TokenHash" character varying(64) NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "ExpiresAt" timestamp with time zone NOT NULL,
    "RevokedAt" timestamp with time zone,
    "ReplacedById" uuid,
    CONSTRAINT "CK_AuthenticationSession_Expiry" CHECK (("ExpiresAt" > "CreatedAt")),
    CONSTRAINT "CK_AuthenticationSession_Hash" CHECK ((("TokenHash")::text ~ '^[0-9a-f]{64}$'::text)),
    CONSTRAINT "CK_AuthenticationSession_Rotation" CHECK ((("ReplacedById" IS NULL) OR (("RevokedAt" IS NOT NULL) AND ("ReplacedById" <> "AuthenticationSessionId"))))
);


--
-- Name: BusinessCalendar; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."BusinessCalendar" (
    "CalendarId" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "TimeZone" character varying(64) NOT NULL,
    "WorkingHoursJson" jsonb NOT NULL,
    "HolidaysJson" jsonb NOT NULL,
    CONSTRAINT "CK_BusinessCalendar_Holidays" CHECK ((jsonb_typeof("HolidaysJson") = 'array'::text)),
    CONSTRAINT "CK_BusinessCalendar_Hours" CHECK ((jsonb_typeof("WorkingHoursJson") = 'object'::text))
);


--
-- Name: Comment; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."Comment" (
    "CommentId" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "ObjectType" character varying(32) NOT NULL,
    "ObjectId" uuid NOT NULL,
    "AuthorId" uuid NOT NULL,
    "Content" text NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "EditedAt" timestamp with time zone,
    "DeletedAt" timestamp with time zone,
    CONSTRAINT "CK_Comment_ObjectType" CHECK ((("ObjectType")::text = ANY ((ARRAY['TASK'::character varying, 'REQUEST'::character varying, 'RESULT'::character varying, 'PROGRESS'::character varying])::text[])))
);


--
-- Name: Company; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."Company" (
    "CompanyId" uuid NOT NULL,
    "Code" character varying(50) NOT NULL,
    "Name" character varying(200) NOT NULL,
    "ContactEmail" character varying(320) NOT NULL,
    "Status" public.company_status NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "TaxId" character varying(50) DEFAULT ''::character varying NOT NULL,
    "ContactPersonName" character varying(150) DEFAULT ''::character varying NOT NULL,
    "ContactPhone" character varying(30) DEFAULT ''::character varying NOT NULL,
    "EvidenceDocumentKey" character varying(500),
    "EvidenceDocumentFileName" character varying(255)
);


--
-- Name: Confirmation; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."Confirmation" (
    "ConfirmationId" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "ObjectType" text DEFAULT 'TASK'::text NOT NULL,
    "ObjectId" uuid NOT NULL,
    "MilestoneType" text DEFAULT 'RESULT'::text NOT NULL,
    "ActorId" uuid NOT NULL,
    "Decision" text DEFAULT 'CONFIRMED'::text NOT NULL,
    "Note" text,
    "ConfirmedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "CK_Confirmation_Decision" CHECK (("Decision" = ANY (ARRAY['CONFIRMED'::text, 'REJECTED'::text]))),
    CONSTRAINT "CK_Confirmation_Milestone" CHECK (("MilestoneType" = ANY (ARRAY['RECEIVE'::text, 'RESULT'::text, 'RESOLUTION'::text]))),
    CONSTRAINT "CK_Confirmation_ObjectType" CHECK (("ObjectType" = ANY (ARRAY['TASK'::text, 'REQUEST'::text])))
);


--
-- Name: DeadlineReminderSchedule; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."DeadlineReminderSchedule" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "TaskId" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "LeadTimeMinutes" integer NOT NULL,
    "RemindAt" timestamp with time zone NOT NULL,
    "TriggerType" character varying(50) NOT NULL,
    "Status" character varying(30) NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL
);


--
-- Name: Department; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."Department" (
    "DepartmentId" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "Code" character varying(50) NOT NULL,
    "Name" character varying(200) NOT NULL,
    "ParentDepartmentId" uuid,
    "Status" public.record_status NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "CK_Department_NoSelfParent" CHECK ((("ParentDepartmentId" IS NULL) OR ("ParentDepartmentId" <> "DepartmentId")))
);


--
-- Name: ManagementScope; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."ManagementScope" (
    "ManagementScopeId" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "DepartmentId" uuid NOT NULL,
    "IncludeDescendants" boolean NOT NULL,
    "CreatedBy" uuid NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL
);


--
-- Name: Notification; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."Notification" (
    "NotificationId" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "RecipientId" uuid NOT NULL,
    "Type" character varying(80) NOT NULL,
    "ObjectType" character varying(40),
    "ObjectId" uuid,
    "Title" character varying(200) NOT NULL,
    "Content" text NOT NULL,
    "ReadAt" timestamp with time zone,
    "SentAt" timestamp with time zone,
    "IdempotencyKey" character varying(180) NOT NULL,
    CONSTRAINT "CK_Notification_Text" CHECK (((length(btrim(("Title")::text)) > 0) AND (length(btrim("Content")) > 0) AND (length(btrim(("IdempotencyKey")::text)) > 0))),
    CONSTRAINT "CK_Notification_Type" CHECK ((("Type")::text = ANY ((ARRAY['TaskAssigned'::character varying, 'TaskAccepted'::character varying, 'TaskRejected'::character varying, 'ProgressSubmitted'::character varying, 'ResultSubmitted'::character varying, 'TaskConfirmed'::character varying, 'RequestSubmitted'::character varying, 'RequestRouted'::character varying, 'RequestReceived'::character varying, 'RequestResolved'::character varying, 'RequestRejected'::character varying, 'RequestRevised'::character varying, 'DeadlineWarning'::character varying, 'Overdue'::character varying, 'Escalation'::character varying, 'ApprovalRequired'::character varying])::text[])))
);


--
-- Name: NotificationLog; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."NotificationLog" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "TaskId" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "TriggerType" character varying(50) NOT NULL,
    "ScheduledAt" timestamp with time zone NOT NULL,
    "Channel" character varying(30) NOT NULL,
    "Status" character varying(30) NOT NULL,
    "DeliveredAt" timestamp with time zone NOT NULL,
    "ErrorMessage" character varying(1000)
);


--
-- Name: Permission; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."Permission" (
    "PermissionId" uuid NOT NULL,
    "Code" character varying(120) NOT NULL,
    "Module" character varying(80) NOT NULL,
    "Action" character varying(80) NOT NULL,
    "ScopeType" public.permission_scope NOT NULL
);


--
-- Name: Request; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."Request" (
    "RequestId" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "RequesterId" uuid NOT NULL,
    "ServiceId" uuid NOT NULL,
    "CategoryId" uuid NOT NULL,
    "ParentRequestId" uuid,
    "RevisedFromRequestId" uuid,
    "Title" character varying(300) NOT NULL,
    "Description" text NOT NULL,
    "Priority" public.request_priority DEFAULT 'MEDIUM'::public.request_priority NOT NULL,
    "Status" public.request_state DEFAULT 'DRAFT'::public.request_state NOT NULL,
    "WorkflowVersionId" uuid,
    "SLAVersionId" uuid,
    "ResolvedAt" timestamp with time zone,
    "ClosedAt" timestamp with time zone,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "DeletedAt" timestamp with time zone,
    CONSTRAINT "CK_Request_Description" CHECK ((length(btrim("Description")) > 0)),
    CONSTRAINT "CK_Request_Parent" CHECK ((("ParentRequestId" IS NULL) OR ("ParentRequestId" <> "RequestId"))),
    CONSTRAINT "CK_Request_Revision" CHECK ((("RevisedFromRequestId" IS NULL) OR ("RevisedFromRequestId" <> "RequestId"))),
    CONSTRAINT "CK_Request_Title" CHECK (((length(btrim(("Title")::text)) >= 1) AND (length(btrim(("Title")::text)) <= 300)))
);


--
-- Name: RequestResolution; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."RequestResolution" (
    "ResolutionId" uuid NOT NULL,
    "RequestId" uuid NOT NULL,
    "ResolverId" uuid NOT NULL,
    "Content" text NOT NULL,
    "RevisionNo" integer DEFAULT 1 NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "CK_RequestResolution_Content" CHECK ((length(btrim("Content")) > 0)),
    CONSTRAINT "CK_RequestResolution_Revision" CHECK (("RevisionNo" > 0))
);


--
-- Name: RequestRouting; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."RequestRouting" (
    "RequestRoutingId" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "RequestId" uuid NOT NULL,
    "FromDepartmentId" uuid,
    "ToDepartmentId" uuid,
    "FromUserId" uuid,
    "ToUserId" uuid,
    "RoutedBy" uuid NOT NULL,
    "RoutedAt" timestamp with time zone NOT NULL,
    "Reason" character varying(2000),
    "Source" character varying(20) NOT NULL,
    CONSTRAINT "CK_RequestRouting_Source" CHECK ((("Source")::text = ANY ((ARRAY['MANUAL'::character varying, 'AI'::character varying, 'RULE'::character varying])::text[])))
);


--
-- Name: Role; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."Role" (
    "RoleId" uuid NOT NULL,
    "TenantId" uuid,
    "Name" character varying(100) NOT NULL,
    "IsSystem" boolean NOT NULL,
    "Status" public.record_status NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "CK_Role_Scope" CHECK ((("IsSystem" AND ("TenantId" IS NULL)) OR ((NOT "IsSystem") AND ("TenantId" IS NOT NULL))))
);


--
-- Name: RolePermission; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."RolePermission" (
    "RoleId" uuid NOT NULL,
    "PermissionId" uuid NOT NULL
);


--
-- Name: SLAProfile; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."SLAProfile" (
    "SLAProfileId" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "Name" character varying(200) NOT NULL,
    "Status" public.sla_profile_status NOT NULL,
    CONSTRAINT "CK_SLAProfile_Name" CHECK (((length(btrim(("Name")::text)) >= 1) AND (length(btrim(("Name")::text)) <= 200)))
);


--
-- Name: SLAVersion; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."SLAVersion" (
    "SLAVersionId" uuid NOT NULL,
    "SLAProfileId" uuid NOT NULL,
    "VersionNo" integer NOT NULL,
    "TargetMinutes" integer NOT NULL,
    "WarningMinutes" integer NOT NULL,
    "CalendarId" uuid NOT NULL,
    "EscalationConfigJson" jsonb,
    CONSTRAINT "CK_SLAVersion_Escalation" CHECK ((("EscalationConfigJson" IS NULL) OR (jsonb_typeof("EscalationConfigJson") = 'object'::text))),
    CONSTRAINT "CK_SLAVersion_Number" CHECK (("VersionNo" > 0)),
    CONSTRAINT "CK_SLAVersion_Thresholds" CHECK ((("TargetMinutes" > 0) AND ("WarningMinutes" >= 0) AND ("WarningMinutes" < "TargetMinutes")))
);


--
-- Name: Service; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."Service" (
    "ServiceId" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "Code" character varying(80) NOT NULL,
    "Name" character varying(200) NOT NULL,
    "Description" text,
    "Status" public.service_status NOT NULL,
    "ActiveWorkflowVersionId" uuid,
    "ActiveSLAVersionId" uuid,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "CK_Service_Code" CHECK (((length(btrim(("Code")::text)) >= 1) AND (length(btrim(("Code")::text)) <= 80))),
    CONSTRAINT "CK_Service_Name" CHECK (((length(btrim(("Name")::text)) >= 1) AND (length(btrim(("Name")::text)) <= 200)))
);


--
-- Name: ServiceCategory; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."ServiceCategory" (
    "ServiceCategoryId" uuid NOT NULL,
    "ServiceId" uuid NOT NULL,
    "Code" character varying(80) NOT NULL,
    "Name" character varying(200) NOT NULL,
    "Status" public.service_category_status NOT NULL,
    CONSTRAINT "CK_ServiceCategory_Code" CHECK (((length(btrim(("Code")::text)) >= 1) AND (length(btrim(("Code")::text)) <= 80))),
    CONSTRAINT "CK_ServiceCategory_Name" CHECK (((length(btrim(("Name")::text)) >= 1) AND (length(btrim(("Name")::text)) <= 200)))
);


--
-- Name: Task; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."Task" (
    "TaskId" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "RequestId" uuid,
    "CreatorId" uuid NOT NULL,
    "Title" character varying(300) NOT NULL,
    "Description" text,
    "Priority" public.task_priority DEFAULT 'MEDIUM'::public.task_priority NOT NULL,
    "Deadline" timestamp with time zone,
    "Status" public.task_state DEFAULT 'DRAFT'::public.task_state NOT NULL,
    "WorkflowVersionId" uuid,
    "SLAVersionId" uuid,
    "CompletedAt" timestamp with time zone,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "DeletedAt" timestamp with time zone,
    CONSTRAINT "CK_Task_Title" CHECK (((length(btrim(("Title")::text)) >= 1) AND (length(btrim(("Title")::text)) <= 300)))
);


--
-- Name: TaskAssignment; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."TaskAssignment" (
    "TaskAssignmentId" uuid NOT NULL,
    "TaskId" uuid NOT NULL,
    "DepartmentId" uuid,
    "UserId" uuid,
    "AssignedBy" uuid NOT NULL,
    "AssignedAt" timestamp with time zone NOT NULL,
    "AcceptedAt" timestamp with time zone,
    "RejectedAt" timestamp with time zone,
    "RejectionReason" text,
    "EndedAt" timestamp with time zone,
    CONSTRAINT "CK_TaskAssignment_Reason" CHECK (((("RejectedAt" IS NULL) AND ("RejectionReason" IS NULL)) OR (("RejectedAt" IS NOT NULL) AND ("RejectionReason" IS NOT NULL) AND (length(btrim("RejectionReason")) > 0)))),
    CONSTRAINT "CK_TaskAssignment_Receipt" CHECK ((NOT (("AcceptedAt" IS NOT NULL) AND ("RejectedAt" IS NOT NULL)))),
    CONSTRAINT "CK_TaskAssignment_Target" CHECK ((("DepartmentId" IS NOT NULL) OR ("UserId" IS NOT NULL))),
    CONSTRAINT "CK_TaskAssignment_Times" CHECK (((("AcceptedAt" IS NULL) OR ("AcceptedAt" >= "AssignedAt")) AND (("RejectedAt" IS NULL) OR ("RejectedAt" >= "AssignedAt")) AND (("EndedAt" IS NULL) OR (("EndedAt" >= "AssignedAt") AND (("AcceptedAt" IS NULL) OR ("AcceptedAt" <= "EndedAt")) AND (("RejectedAt" IS NULL) OR ("RejectedAt" <= "EndedAt"))))))
);


--
-- Name: TaskChecklistItem; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."TaskChecklistItem" (
    "ChecklistItemId" uuid NOT NULL,
    "TaskId" uuid NOT NULL,
    "Title" character varying(300) NOT NULL,
    "SortOrder" integer DEFAULT 1 NOT NULL,
    "IsCompleted" boolean DEFAULT false NOT NULL,
    "CompletedBy" uuid,
    "CompletedAt" timestamp with time zone,
    CONSTRAINT "CK_TaskChecklistItem_Completion" CHECK ((("IsCompleted" AND ("CompletedBy" IS NOT NULL) AND ("CompletedAt" IS NOT NULL)) OR ((NOT "IsCompleted") AND ("CompletedBy" IS NULL) AND ("CompletedAt" IS NULL)))),
    CONSTRAINT "CK_TaskChecklistItem_Order" CHECK (("SortOrder" > 0)),
    CONSTRAINT "CK_TaskChecklistItem_Title" CHECK (((length(btrim(("Title")::text)) >= 1) AND (length(btrim(("Title")::text)) <= 300)))
);


--
-- Name: TaskProgressReport; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."TaskProgressReport" (
    "ProgressReportId" uuid NOT NULL,
    "TaskId" uuid NOT NULL,
    "AuthorId" uuid NOT NULL,
    "Percent" smallint DEFAULT 0 NOT NULL,
    "Content" text,
    "SubmittedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "CK_TaskProgressReport_Percent" CHECK ((("Percent" >= 0) AND ("Percent" <= 100)))
);


--
-- Name: TaskResult; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."TaskResult" (
    "TaskResultId" uuid NOT NULL,
    "TaskId" uuid NOT NULL,
    "AuthorId" uuid NOT NULL,
    "Content" text,
    "RevisionNo" integer DEFAULT 1 NOT NULL,
    "SubmittedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "CK_TaskResult_Revision" CHECK (("RevisionNo" > 0))
);


--
-- Name: Tenant; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."Tenant" (
    "TenantId" uuid NOT NULL,
    "CompanyId" uuid NOT NULL,
    "TenantKey" character varying(80) NOT NULL,
    "Name" character varying(200) NOT NULL,
    "Status" public.tenant_status NOT NULL,
    "TimeZone" character varying(64) NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL
);


--
-- Name: TenantSetting; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."TenantSetting" (
    "TenantSettingId" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "Key" character varying(128) NOT NULL,
    "ValueJson" jsonb NOT NULL,
    "UpdatedBy" uuid NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "CK_TenantSetting_Value" CHECK (public.bizflow_valid_tenant_setting(("Key")::text, "ValueJson"))
);


--
-- Name: User; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."User" (
    "UserId" uuid NOT NULL,
    "TenantId" uuid,
    "IsPlatformAdministrator" boolean NOT NULL,
    "EmployeeCode" character varying(50) NOT NULL,
    "NormalizedEmployeeCode" character varying(50) NOT NULL,
    "Email" character varying(320) NOT NULL,
    "NormalizedEmail" character varying(320) NOT NULL,
    "PasswordHash" text NOT NULL,
    "SecurityStamp" character varying(64) NOT NULL,
    "FullName" character varying(200) NOT NULL,
    "DepartmentId" uuid,
    "Status" public.user_status NOT NULL,
    "AccessFailedCount" integer NOT NULL,
    "LockoutEnd" timestamp with time zone,
    "MustChangePassword" boolean NOT NULL,
    "LastLoginAt" timestamp with time zone,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "DeletedAt" timestamp with time zone,
    CONSTRAINT "CK_User_AccessFailedCount" CHECK (("AccessFailedCount" >= 0)),
    CONSTRAINT "CK_User_AccountPlane" CHECK (((("TenantId" IS NULL) AND "IsPlatformAdministrator" AND ("DepartmentId" IS NULL)) OR (("TenantId" IS NOT NULL) AND (NOT "IsPlatformAdministrator"))))
);


--
-- Name: UserRole; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."UserRole" (
    "UserId" uuid NOT NULL,
    "RoleId" uuid NOT NULL
);


--
-- Name: WebPushSubscription; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."WebPushSubscription" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "Endpoint" character varying(1000) NOT NULL,
    "P256dh" character varying(250) NOT NULL,
    "Auth" character varying(150) NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "LastActiveAt" timestamp with time zone NOT NULL
);


--
-- Name: Workflow; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."Workflow" (
    "WorkflowId" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "Name" character varying(200) NOT NULL,
    "BusinessType" public.workflow_business_type NOT NULL,
    "Status" public.workflow_status NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL
);


--
-- Name: WorkflowStep; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."WorkflowStep" (
    "WorkflowStepId" uuid NOT NULL,
    "WorkflowVersionId" uuid NOT NULL,
    "StepCode" character varying(80) NOT NULL,
    "Name" character varying(200) NOT NULL,
    "Type" public.workflow_step_type NOT NULL,
    "OrderNo" integer NOT NULL,
    "ConfigJson" jsonb NOT NULL,
    CONSTRAINT "CK_WorkflowStep_Code" CHECK ((("StepCode")::text ~ '^[A-Z0-9_.-]{1,80}$'::text)),
    CONSTRAINT "CK_WorkflowStep_JsonObject" CHECK ((jsonb_typeof("ConfigJson") = 'object'::text)),
    CONSTRAINT "CK_WorkflowStep_Order" CHECK (("OrderNo" > 0))
);


--
-- Name: WorkflowTransition; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."WorkflowTransition" (
    "TransitionId" uuid NOT NULL,
    "WorkflowVersionId" uuid NOT NULL,
    "FromState" character varying(50) NOT NULL,
    "ToState" character varying(50) NOT NULL,
    "GuardJson" jsonb,
    CONSTRAINT "CK_WorkflowTransition_JsonObject" CHECK ((("GuardJson" IS NULL) OR (jsonb_typeof("GuardJson") = 'object'::text)))
);


--
-- Name: WorkflowVersion; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."WorkflowVersion" (
    "WorkflowVersionId" uuid NOT NULL,
    "WorkflowId" uuid NOT NULL,
    "VersionNo" integer NOT NULL,
    "Status" public.workflow_version_status NOT NULL,
    "PublishedAt" timestamp with time zone,
    "DefinitionJson" jsonb NOT NULL,
    CONSTRAINT "CK_WorkflowVersion_JsonObject" CHECK ((jsonb_typeof("DefinitionJson") = 'object'::text)),
    CONSTRAINT "CK_WorkflowVersion_Number" CHECK (("VersionNo" > 0)),
    CONSTRAINT "CK_WorkflowVersion_Publication" CHECK (((("Status" = 'DRAFT'::public.workflow_version_status) AND ("PublishedAt" IS NULL)) OR (("Status" <> 'DRAFT'::public.workflow_version_status) AND ("PublishedAt" IS NOT NULL))))
);


--
-- Name: __EFMigrationsHistory; Type: TABLE; Schema: public; Owner: -
--

CREATE TABLE public."__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL
);


--
-- Data for Name: AIAgentAction; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public."AIAgentAction" ("AIAgentActionId", "TenantId", "AIInteractionId", "ToolName", "ArgsJson", "AuthorizationResult", "ExecutionStatus", "ResultRef", "CreatedAt") FROM stdin;
\.


--
-- Data for Name: AIInteraction; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public."AIInteraction" ("AIInteractionId", "TenantId", "UserId", "Feature", "ModelName", "InputRef", "OutputRef", "Status", "LatencyMs", "TokenUsage", "CreatedAt") FROM stdin;
\.


--
-- Data for Name: AIRecommendation; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public."AIRecommendation" ("RecommendationId", "TenantId", "AIInteractionId", "ObjectType", "ObjectId", "RecommendationType", "PayloadJson", "Confidence", "HumanDecision", "DecisionBy", "DecidedAt") FROM stdin;
\.


--
-- Data for Name: AIVectorMemory; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public."AIVectorMemory" ("MemoryId", "TenantId", "MemoryScope", "Title", "Content", "MetadataJson", "EmbeddingJson", "CreatedAt", "UpdatedAt") FROM stdin;
019f8000-0000-7000-8000-000000000080	019f8000-0000-7000-8000-000000000002	POLICY	Chính sách tạm ứng, thanh toán và quyết toán công tác phí	Quy định tạm ứng và quyết toán công tác phí tại BizFlow Corporation: 1. Nhân viên được tạm ứng tối đa 80% chi phí dự kiến trước ngày khởi hành ít nhất 3 ngày làm việc. 2. Định mức lưu trú khách sạn: Cán bộ quản lý tối đa 1.500.000 VNĐ/đêm, nhân viên tối đa 1.000.000 VNĐ/đêm tại Hà Nội và TP.HCM; các tỉnh thành khác tối đa 800.000 VNĐ/đêm. 3. Vé máy bay: Mặc định hạng Phổ thông (Economy), ưu tiên đặt trước ít nhất 7 ngày. 4. Thời hạn hoàn ứng: Nộp đầy đủ hóa đơn GTGT hợp lệ và bảng kê chi phí trong vòng 7 ngày làm việc kể từ ngày kết thúc công tác về Phòng Tài chính - Kế toán.	{"version": "2026.1", "category": "FINANCE", "department": "FIN"}	[0.037452, 0.0, 0.056178, 0.046815, 0.0, 0.028089, 0.0, 0.0, 0.028089, 0.037452, 0.0, 0.0, 0.0, 0.18726, 0.0, 0.0, 0.046815, 0.121719, 0.0, 0.112356, 0.046815, 0.056178, 0.0, 0.0, 0.0, 0.0, 0.028089, 0.037452, 0.131082, 0.009363, 0.028089, 0.037452, 0.0, 0.0, 0.028089, 0.046815, 0.065541, 0.028089, 0.0, 0.028089, 0.028089, 0.037452, 0.0, 0.009363, 0.112356, 0.0, 0.0, 0.028089, 0.018726, 0.112356, 0.028089, 0.215349, 0.0, 0.074904, 0.037452, 0.0, 0.009363, 0.0, 0.028089, 0.018726, 0.028089, 0.009363, 0.037452, 0.037452, 0.0, 0.0, 0.028089, 0.028089, 0.0, 0.037452, 0.0, 0.0, 0.0, 0.140445, 0.0, 0.0, 0.028089, 0.028089, 0.0, 0.028089, 0.046815, 0.0, 0.09363, 0.009363, 0.0, 0.205986, 0.0, 0.0, 0.028089, 0.0, 0.121719, 0.028089, 0.0, 0.09363, 0.037452, 0.0, 0.0, 0.084267, 0.0, 0.037452, 0.0, 0.018726, 0.0, 0.028089, 0.065541, 0.0, 0.0, 0.0, 0.0, 0.037452, 0.0, 0.0, 0.037452, 0.074904, 0.0, 0.0, 0.0, 0.149808, 0.037452, 0.0, 0.0, 0.074904, 0.074904, 0.028089, 0.018726, 0.0, 0.018726, 0.09363, 0.0, 0.056178, 0.056178, 0.009363, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.065541, 0.028089, 0.0, 0.0, 0.084267, 0.0, 0.112356, 0.140445, 0.0, 0.0, 0.09363, 0.084267, 0.0, 0.028089, 0.056178, 0.046815, 0.0, 0.09363, 0.0, 0.009363, 0.037452, 0.0, 0.009363, 0.159171, 0.0, 0.028089, 0.0, 0.056178, 0.0, 0.0, 0.0, 0.028089, 0.028089, 0.0, 0.009363, 0.0, 0.037452, 0.028089, 0.112356, 0.028089, 0.065541, 0.131082, 0.028089, 0.0, 0.028089, 0.0, 0.0, 0.224712, 0.009363, 0.056178, 0.102993, 0.0, 0.0, 0.028089, 0.009363, 0.074904, 0.074904, 0.037452, 0.0, 0.009363, 0.0, 0.037452, 0.0, 0.065541, 0.0, 0.0, 0.009363, 0.037452, 0.037452, 0.0, 0.028089, 0.009363, 0.0, 0.0, 0.037452, 0.0, 0.102993, 0.0, 0.074904, 0.028089, 0.0, 0.028089, 0.037452, 0.037452, 0.0, 0.0, 0.056178, 0.0, 0.056178, 0.149808, 0.028089, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.037452, 0.037452, 0.0, 0.028089, 0.046815, 0.0, 0.074904, 0.0, 0.0, 0.056178, 0.037452, 0.074904, 0.0, 0.009363, 0.0, 0.0, 0.046815, 0.0, 0.0, 0.0, 0.009363, 0.0, 0.046815, 0.028089, 0.0, 0.0, 0.18726, 0.056178, 0.0, 0.0, 0.0, 0.009363, 0.009363, 0.037452, 0.028089, 0.084267, 0.0, 0.0, 0.0, 0.037452, 0.0, 0.084267, 0.028089, 0.0, 0.112356, 0.0, 0.0, 0.018726, 0.074904, 0.028089, 0.084267, 0.0, 0.0, 0.102993, 0.028089, 0.0, 0.0, 0.037452, 0.0, 0.0, 0.0, 0.037452, 0.009363, 0.037452, 0.028089, 0.028089, 0.0, 0.0, 0.028089, 0.028089, 0.0, 0.0, 0.009363, 0.0, 0.0, 0.0, 0.018726, 0.0, 0.0, 0.0, 0.0, 0.028089, 0.028089, 0.028089, 0.0, 0.0, 0.028089, 0.018726, 0.028089, 0.028089, 0.0, 0.0, 0.0, 0.215349, 0.065541, 0.196623, 0.0, 0.0, 0.018726, 0.046815, 0.037452, 0.046815, 0.0, 0.0, 0.009363, 0.0, 0.0, 0.074904, 0.0, 0.009363, 0.0, 0.037452, 0.0, 0.037452, 0.028089, 0.028089, 0.0, 0.0, 0.046815, 0.0, 0.065541, 0.084267, 0.028089, 0.046815, 0.215349, 0.037452, 0.028089, 0.0, 0.046815, 0.0, 0.0, 0.028089, 0.0, 0.028089, 0.056178, 0.102993, 0.037452, 0.0, 0.0, 0.037452, 0.0, 0.028089, 0.0, 0.0, 0.037452, 0.084267, 0.028089, 0.037452, 0.0]	2026-10-22 17:13:19.974202+00	2026-11-01 17:13:19.974202+00
019f8000-0000-7000-8000-000000000081	019f8000-0000-7000-8000-000000000002	POLICY	Quy định làm việc từ xa và văn hóa công sở BizFlow	Quy định làm việc từ xa (Remote Work) tại BizFlow Corporation: 1. Nhân sự đã qua thử việc được đăng ký tối đa 2 ngày làm việc từ xa mỗi tuần với sự phê duyệt của Quản lý phòng ban. 2. Khung giờ làm việc cốt lõi bắt buộc phải trực tuyến trên hệ thống và kênh chat nội bộ: Sáng 08:30 - 12:00, Chiều 13:30 - 17:30. 3. Tuân thủ bảo mật: Không sử dụng mạng Wi-Fi công cộng không có bảo mật để truy cập cơ sở dữ liệu nội bộ. Phải sử dụng VPN công ty khi cần truy cập hạ tầng backend.	{"version": "2026.2", "category": "HR", "department": "HR"}	[0.0, 0.0, 0.010993, 0.10993, 0.0, 0.054965, 0.054965, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.087944, 0.0, 0.0, 0.032979, 0.0, 0.0, 0.032979, 0.032979, 0.0, 0.032979, 0.0, 0.0, 0.032979, 0.0, 0.032979, 0.054965, 0.010993, 0.0, 0.208867, 0.0, 0.0, 0.054965, 0.0, 0.120923, 0.032979, 0.0, 0.032979, 0.0, 0.010993, 0.0, 0.010993, 0.032979, 0.076951, 0.065958, 0.043972, 0.010993, 0.043972, 0.0, 0.065958, 0.054965, 0.0, 0.087944, 0.032979, 0.021986, 0.032979, 0.0, 0.0, 0.0, 0.010993, 0.076951, 0.043972, 0.010993, 0.0, 0.043972, 0.0, 0.0, 0.032979, 0.0, 0.0, 0.032979, 0.054965, 0.0, 0.021986, 0.0, 0.043972, 0.0, 0.0, 0.065958, 0.0, 0.153902, 0.0, 0.0, 0.241846, 0.0, 0.0, 0.0, 0.0, 0.010993, 0.0, 0.0, 0.10993, 0.10993, 0.0, 0.043972, 0.10993, 0.0, 0.164895, 0.043972, 0.087944, 0.0, 0.0, 0.0, 0.021986, 0.098937, 0.0, 0.10993, 0.043972, 0.0, 0.032979, 0.054965, 0.076951, 0.0, 0.043972, 0.0, 0.087944, 0.021986, 0.0, 0.0, 0.0, 0.010993, 0.0, 0.065958, 0.010993, 0.0, 0.0, 0.0, 0.032979, 0.0, 0.010993, 0.054965, 0.0, 0.010993, 0.032979, 0.0, 0.043972, 0.241846, 0.032979, 0.0, 0.043972, 0.0, 0.043972, 0.0, 0.0, 0.032979, 0.010993, 0.21986, 0.0, 0.032979, 0.032979, 0.0, 0.087944, 0.065958, 0.087944, 0.010993, 0.0, 0.010993, 0.0, 0.0, 0.0, 0.0, 0.032979, 0.010993, 0.0, 0.0, 0.0, 0.032979, 0.0, 0.043972, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.098937, 0.164895, 0.0, 0.021986, 0.043972, 0.0, 0.054965, 0.0, 0.098937, 0.021986, 0.032979, 0.0, 0.032979, 0.0, 0.065958, 0.021986, 0.010993, 0.098937, 0.098937, 0.032979, 0.010993, 0.032979, 0.0, 0.021986, 0.098937, 0.0, 0.043972, 0.021986, 0.0, 0.065958, 0.032979, 0.043972, 0.032979, 0.065958, 0.054965, 0.0, 0.032979, 0.0, 0.0, 0.032979, 0.0, 0.032979, 0.0, 0.0, 0.032979, 0.065958, 0.0, 0.054965, 0.0, 0.0, 0.0, 0.0, 0.032979, 0.0, 0.0, 0.010993, 0.0, 0.0, 0.010993, 0.043972, 0.054965, 0.043972, 0.0, 0.0, 0.043972, 0.0, 0.032979, 0.032979, 0.0, 0.10993, 0.131916, 0.054965, 0.0, 0.043972, 0.0, 0.032979, 0.0, 0.032979, 0.0, 0.010993, 0.032979, 0.0, 0.010993, 0.032979, 0.0, 0.032979, 0.131916, 0.0, 0.010993, 0.054965, 0.0, 0.120923, 0.032979, 0.21986, 0.0, 0.0, 0.0, 0.208867, 0.0, 0.0, 0.032979, 0.0, 0.131916, 0.0, 0.10993, 0.0, 0.065958, 0.065958, 0.032979, 0.0, 0.043972, 0.0, 0.043972, 0.0, 0.010993, 0.0, 0.0, 0.0, 0.10993, 0.131916, 0.0, 0.120923, 0.0, 0.0, 0.021986, 0.0, 0.032979, 0.065958, 0.0, 0.0, 0.010993, 0.0, 0.010993, 0.0, 0.010993, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.054965, 0.054965, 0.032979, 0.032979, 0.076951, 0.0, 0.032979, 0.0, 0.0, 0.010993, 0.065958, 0.10993, 0.010993, 0.0, 0.098937, 0.010993, 0.0, 0.098937, 0.0, 0.0, 0.032979, 0.0, 0.043972, 0.032979, 0.0, 0.0, 0.032979, 0.0, 0.0, 0.0, 0.0, 0.0, 0.043972, 0.032979, 0.010993, 0.032979, 0.043972, 0.043972, 0.0, 0.0, 0.0, 0.0, 0.0, 0.021986, 0.0, 0.0, 0.0, 0.098937, 0.043972, 0.010993, 0.0, 0.032979, 0.098937, 0.0, 0.0, 0.0, 0.0, 0.0, 0.032979, 0.043972, 0.043972, 0.043972]	2026-10-22 17:13:19.974202+00	2026-11-01 17:13:19.974202+00
019f8000-0000-7000-8000-000000000082	019f8000-0000-7000-8000-000000000002	SLA	Quy trình tiếp nhận và xử lý yêu cầu CNTT (IT Service Desk SLA)	Cam kết chất lượng dịch vụ CNTT (IT Service Level Agreement - SLA): 1. Sự cố mức P1 (Hệ thống ngừng hoạt động, ảnh hưởng toàn bộ doanh nghiệp): Thời gian phản hồi dưới 15 phút, thời gian khắc phục dưới 2 giờ. 2. Sự cố mức P2 (Suy giảm tính năng quan trọng của phòng ban): Phản hồi dưới 30 phút, giải quyết dưới 4 giờ. 3. Yêu cầu cấp phát phần cứng, tài khoản mới (P3): Xử lý trong vòng 24 giờ làm việc. Tất cả yêu cầu cần được tạo trên hệ thống BizFlow Service Desk.	{"version": "2026.1", "category": "IT", "department": "TECH"}	[0.060368, 0.0, 0.012074, 0.012074, 0.072442, 0.012074, 0.108663, 0.036221, 0.012074, 0.0, 0.036221, 0.048295, 0.0, 0.0, 0.072442, 0.0, 0.0, 0.012074, 0.0, 0.0, 0.036221, 0.012074, 0.0, 0.0, 0.0, 0.0, 0.012074, 0.0, 0.156957, 0.048295, 0.0, 0.0, 0.0, 0.0, 0.048295, 0.036221, 0.024147, 0.096589, 0.0, 0.036221, 0.0, 0.0, 0.0, 0.012074, 0.0, 0.0, 0.024147, 0.048295, 0.0, 0.048295, 0.096589, 0.0, 0.0, 0.036221, 0.048295, 0.036221, 0.048295, 0.0, 0.0, 0.0, 0.048295, 0.193178, 0.096589, 0.108663, 0.024147, 0.0, 0.0, 0.060368, 0.0, 0.060368, 0.084515, 0.012074, 0.036221, 0.096589, 0.0, 0.012074, 0.0, 0.036221, 0.0, 0.0, 0.0, 0.048295, 0.012074, 0.048295, 0.156957, 0.108663, 0.012074, 0.0, 0.012074, 0.0, 0.036221, 0.048295, 0.0, 0.181104, 0.0, 0.0, 0.0, 0.048295, 0.036221, 0.048295, 0.144884, 0.012074, 0.0, 0.072442, 0.0, 0.0, 0.0, 0.012074, 0.036221, 0.0, 0.036221, 0.048295, 0.0, 0.0, 0.048295, 0.0, 0.0, 0.048295, 0.072442, 0.0, 0.012074, 0.0, 0.0, 0.012074, 0.084515, 0.024147, 0.012074, 0.012074, 0.0, 0.0, 0.048295, 0.012074, 0.0, 0.0, 0.036221, 0.0, 0.036221, 0.0, 0.120736, 0.0, 0.108663, 0.0, 0.0, 0.012074, 0.048295, 0.0, 0.012074, 0.024147, 0.060368, 0.0, 0.036221, 0.0, 0.048295, 0.0, 0.0, 0.0, 0.0, 0.012074, 0.0, 0.036221, 0.012074, 0.036221, 0.13281, 0.072442, 0.0, 0.0, 0.0, 0.0, 0.036221, 0.0, 0.0, 0.036221, 0.13281, 0.0, 0.0, 0.0, 0.096589, 0.012074, 0.036221, 0.048295, 0.0, 0.048295, 0.036221, 0.072442, 0.0, 0.024147, 0.048295, 0.0, 0.0, 0.048295, 0.012074, 0.0, 0.012074, 0.120736, 0.048295, 0.0, 0.036221, 0.012074, 0.0, 0.096589, 0.096589, 0.036221, 0.012074, 0.0, 0.012074, 0.0, 0.0, 0.036221, 0.072442, 0.048295, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.048295, 0.012074, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.241473, 0.036221, 0.0, 0.0, 0.036221, 0.012074, 0.012074, 0.108663, 0.0, 0.0, 0.036221, 0.0, 0.096589, 0.036221, 0.0, 0.120736, 0.036221, 0.060368, 0.072442, 0.084515, 0.072442, 0.0, 0.0, 0.205252, 0.0, 0.012074, 0.0, 0.048295, 0.048295, 0.072442, 0.012074, 0.0, 0.036221, 0.012074, 0.0, 0.036221, 0.0, 0.024147, 0.048295, 0.036221, 0.0, 0.0, 0.096589, 0.144884, 0.048295, 0.0, 0.048295, 0.012074, 0.13281, 0.036221, 0.084515, 0.0, 0.012074, 0.0, 0.036221, 0.0, 0.0, 0.048295, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.120736, 0.072442, 0.0, 0.181104, 0.024147, 0.072442, 0.060368, 0.0, 0.0, 0.0, 0.0, 0.012074, 0.036221, 0.0, 0.181104, 0.048295, 0.0, 0.0, 0.036221, 0.0, 0.0, 0.012074, 0.0, 0.048295, 0.048295, 0.120736, 0.072442, 0.048295, 0.060368, 0.036221, 0.012074, 0.048295, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.024147, 0.0, 0.0, 0.0, 0.048295, 0.012074, 0.0, 0.120736, 0.072442, 0.036221, 0.156957, 0.036221, 0.0, 0.120736, 0.012074, 0.012074, 0.0, 0.048295, 0.0, 0.0, 0.0, 0.036221, 0.072442, 0.0, 0.0, 0.048295, 0.036221, 0.060368, 0.0, 0.048295, 0.0, 0.0, 0.072442, 0.060368, 0.0, 0.012074, 0.0, 0.120736, 0.0, 0.036221, 0.036221, 0.048295, 0.0, 0.0, 0.096589, 0.0, 0.048295, 0.0, 0.072442, 0.036221, 0.0, 0.048295, 0.012074, 0.0, 0.0, 0.024147, 0.0, 0.036221]	2026-10-22 17:13:19.974202+00	2026-11-01 17:13:19.974202+00
019f8000-0000-7000-8000-000000000083	019f8000-0000-7000-8000-000000000002	SECURITY	Chính sách an toàn thông tin và bảo mật tài khoản người dùng	Chính sách an toàn thông tin BizFlow: 1. Mật khẩu tối thiểu 8 ký tự, bao gồm chữ hoa, chữ thường, chữ số và ký tự đặc biệt. Phải thay đổi mật khẩu định kỳ tối thiểu 90 ngày một lần. 2. Tuyệt đối không chia sẻ tài khoản, mã xác thực 2FA hoặc thông tin đăng nhập cho người khác. 3. Khóa màn hình máy tính khi rời khỏi bàn làm việc (Windows + L). 4. Nghiêm cấm lưu trữ dữ liệu khách hàng hoặc tài liệu mật trên thiết bị cá nhân chưa được phòng CNTT mã hóa.	{"version": "2026.3", "category": "SECURITY", "department": "TECH"}	[0.010679, 0.0, 0.010679, 0.021358, 0.085431, 0.021358, 0.0, 0.074752, 0.0, 0.0, 0.032037, 0.0, 0.117467, 0.053394, 0.0, 0.0, 0.032037, 0.032037, 0.0, 0.0, 0.032037, 0.085431, 0.19222, 0.0, 0.010679, 0.0, 0.0, 0.021358, 0.117467, 0.032037, 0.053394, 0.363081, 0.010679, 0.032037, 0.0, 0.053394, 0.085431, 0.042715, 0.010679, 0.032037, 0.0, 0.032037, 0.032037, 0.010679, 0.09611, 0.0, 0.09611, 0.0, 0.032037, 0.074752, 0.0, 0.170862, 0.042715, 0.0, 0.042715, 0.085431, 0.0, 0.0, 0.0, 0.021358, 0.0, 0.032037, 0.0, 0.042715, 0.0, 0.0, 0.0, 0.0, 0.0, 0.042715, 0.085431, 0.0, 0.010679, 0.064073, 0.053394, 0.053394, 0.09611, 0.021358, 0.0, 0.032037, 0.032037, 0.064073, 0.138825, 0.0, 0.010679, 0.128146, 0.0, 0.0, 0.0, 0.0, 0.010679, 0.032037, 0.0, 0.064073, 0.0, 0.0, 0.0, 0.010679, 0.0, 0.053394, 0.0, 0.010679, 0.085431, 0.0, 0.0, 0.0, 0.085431, 0.0, 0.042715, 0.010679, 0.0, 0.0, 0.042715, 0.010679, 0.042715, 0.0, 0.032037, 0.117467, 0.0, 0.0, 0.0, 0.085431, 0.053394, 0.0, 0.0, 0.0, 0.032037, 0.0, 0.0, 0.0, 0.085431, 0.010679, 0.0, 0.010679, 0.042715, 0.0, 0.032037, 0.0, 0.042715, 0.0, 0.053394, 0.010679, 0.0, 0.032037, 0.032037, 0.0, 0.0, 0.042715, 0.117467, 0.0, 0.0, 0.032037, 0.064073, 0.021358, 0.032037, 0.010679, 0.042715, 0.0, 0.0, 0.0, 0.042715, 0.010679, 0.032037, 0.0, 0.021358, 0.032037, 0.0, 0.010679, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.010679, 0.042715, 0.053394, 0.0, 0.010679, 0.032037, 0.0, 0.0, 0.085431, 0.021358, 0.010679, 0.0, 0.0, 0.064073, 0.010679, 0.0, 0.010679, 0.0, 0.042715, 0.064073, 0.0, 0.010679, 0.032037, 0.042715, 0.021358, 0.0, 0.0, 0.085431, 0.085431, 0.053394, 0.085431, 0.0, 0.0, 0.010679, 0.032037, 0.0, 0.160183, 0.010679, 0.0, 0.042715, 0.0, 0.074752, 0.053394, 0.0, 0.0, 0.032037, 0.0, 0.010679, 0.010679, 0.0, 0.032037, 0.0, 0.0, 0.0, 0.0, 0.0, 0.085431, 0.042715, 0.106789, 0.0, 0.0, 0.053394, 0.085431, 0.0, 0.0, 0.053394, 0.0, 0.0, 0.010679, 0.085431, 0.0, 0.0, 0.032037, 0.074752, 0.0, 0.032037, 0.0, 0.0, 0.0, 0.032037, 0.064073, 0.010679, 0.0, 0.0, 0.0, 0.032037, 0.117467, 0.021358, 0.0, 0.0, 0.0, 0.0, 0.0, 0.053394, 0.042715, 0.0, 0.010679, 0.0, 0.074752, 0.0, 0.0, 0.0, 0.042715, 0.0, 0.09611, 0.138825, 0.0, 0.0, 0.064073, 0.117467, 0.0, 0.032037, 0.021358, 0.010679, 0.0, 0.053394, 0.0, 0.0, 0.010679, 0.0, 0.053394, 0.0, 0.042715, 0.032037, 0.0, 0.0, 0.032037, 0.0, 0.010679, 0.032037, 0.0, 0.0, 0.0, 0.032037, 0.0, 0.010679, 0.0, 0.0, 0.0, 0.0, 0.0, 0.032037, 0.0, 0.010679, 0.0, 0.0, 0.0, 0.032037, 0.0, 0.0, 0.042715, 0.0, 0.0, 0.032037, 0.0, 0.010679, 0.0, 0.021358, 0.117467, 0.010679, 0.405797, 0.0, 0.032037, 0.0, 0.0, 0.0, 0.032037, 0.0, 0.010679, 0.0, 0.0, 0.0, 0.0, 0.032037, 0.0, 0.0, 0.032037, 0.0, 0.064073, 0.064073, 0.0, 0.032037, 0.0, 0.010679, 0.0, 0.0, 0.0, 0.021358, 0.042715, 0.0, 0.0, 0.010679, 0.032037, 0.0, 0.042715, 0.181541, 0.074752, 0.0, 0.064073, 0.0, 0.053394, 0.0, 0.053394, 0.085431, 0.117467, 0.0, 0.0, 0.09611]	2026-10-22 17:13:19.974202+00	2026-11-01 17:13:19.974202+00
019f8000-0000-7000-8000-000000000084	019f8000-0000-7000-8000-000000000002	PROCEDURE	Quy trình Onboarding và cấp phát tài sản cho nhân sự mới	Quy trình tiếp nhận và đào tạo nhân viên mới (Onboarding Flow): 1. Trước ngày gia nhập 3 ngày: Phòng Nhân sự gửi yêu cầu cấp phát laptop, tạo email doanh nghiệp và thẻ ra vào cho Phòng CNTT. 2. Ngày đầu tiên (Day 1): Gặp gỡ người hướng dẫn (Buddy), tham quan văn phòng, ký kết hợp đồng thử việc và nhận bàn giao tài sản. 3. Tuần đầu tiên: Hoàn thành khóa học trực tuyến về Quy chế văn hóa doanh nghiệp và Hướng dẫn sử dụng nền tảng BizFlow. 4. Sau 60 ngày: Phòng Nhân sự tổ chức đánh giá kết quả thử việc cùng Trưởng phòng ban.	{"version": "2026.1", "category": "HR", "department": "HR"}	[0.042052, 0.0, 0.031539, 0.042052, 0.0, 0.063078, 0.084103, 0.105129, 0.0, 0.126155, 0.0, 0.0, 0.0, 0.17872, 0.0, 0.0, 0.0, 0.07359, 0.0, 0.0, 0.042052, 0.0, 0.0, 0.031539, 0.115642, 0.0, 0.010513, 0.031539, 0.136668, 0.010513, 0.0, 0.0, 0.0, 0.0, 0.094616, 0.115642, 0.021026, 0.0, 0.010513, 0.0, 0.0, 0.042052, 0.010513, 0.052565, 0.0, 0.0, 0.0, 0.031539, 0.0, 0.042052, 0.0, 0.052565, 0.136668, 0.0, 0.042052, 0.0, 0.0, 0.0, 0.0, 0.0, 0.052565, 0.0, 0.0, 0.136668, 0.0, 0.031539, 0.0, 0.010513, 0.0, 0.0, 0.031539, 0.021026, 0.0, 0.063078, 0.010513, 0.0, 0.031539, 0.010513, 0.0, 0.0, 0.0, 0.0, 0.021026, 0.031539, 0.063078, 0.042052, 0.0, 0.0, 0.0, 0.0, 0.042052, 0.010513, 0.010513, 0.052565, 0.0, 0.0, 0.0, 0.084103, 0.0, 0.052565, 0.147181, 0.031539, 0.031539, 0.042052, 0.010513, 0.010513, 0.052565, 0.094616, 0.0, 0.052565, 0.031539, 0.031539, 0.031539, 0.07359, 0.07359, 0.031539, 0.0, 0.241797, 0.042052, 0.0, 0.031539, 0.042052, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.031539, 0.105129, 0.0, 0.0, 0.0, 0.042052, 0.0, 0.031539, 0.199746, 0.0, 0.0, 0.0, 0.0, 0.063078, 0.084103, 0.0, 0.063078, 0.0, 0.031539, 0.0, 0.0, 0.0, 0.031539, 0.0, 0.0, 0.0, 0.0, 0.126155, 0.0, 0.105129, 0.0, 0.010513, 0.0, 0.031539, 0.0, 0.031539, 0.0, 0.105129, 0.0, 0.063078, 0.010513, 0.0, 0.052565, 0.042052, 0.0, 0.0, 0.115642, 0.0, 0.021026, 0.031539, 0.0, 0.0, 0.0, 0.0, 0.052565, 0.010513, 0.010513, 0.042052, 0.084103, 0.021026, 0.052565, 0.0, 0.021026, 0.0, 0.084103, 0.07359, 0.0, 0.0, 0.0, 0.031539, 0.0, 0.031539, 0.063078, 0.07359, 0.010513, 0.042052, 0.0, 0.0, 0.0, 0.021026, 0.031539, 0.0, 0.031539, 0.010513, 0.115642, 0.0, 0.094616, 0.0, 0.0, 0.042052, 0.0, 0.031539, 0.0, 0.0, 0.021026, 0.084103, 0.052565, 0.031539, 0.0, 0.0, 0.031539, 0.031539, 0.042052, 0.031539, 0.042052, 0.094616, 0.031539, 0.031539, 0.0, 0.0, 0.0, 0.17872, 0.0, 0.0, 0.0, 0.0, 0.07359, 0.168207, 0.052565, 0.021026, 0.0, 0.031539, 0.084103, 0.0, 0.031539, 0.0, 0.021026, 0.021026, 0.0, 0.0, 0.0, 0.010513, 0.0, 0.042052, 0.0, 0.031539, 0.021026, 0.0, 0.0, 0.105129, 0.115642, 0.0, 0.010513, 0.0, 0.105129, 0.031539, 0.021026, 0.0, 0.0, 0.0, 0.0, 0.0, 0.052565, 0.0, 0.042052, 0.0, 0.0, 0.063078, 0.0, 0.010513, 0.0, 0.031539, 0.0, 0.0, 0.021026, 0.0, 0.031539, 0.094616, 0.0, 0.094616, 0.094616, 0.0, 0.052565, 0.063078, 0.0, 0.031539, 0.0, 0.0, 0.0, 0.031539, 0.0, 0.063078, 0.115642, 0.042052, 0.084103, 0.0, 0.010513, 0.0, 0.084103, 0.021026, 0.0, 0.094616, 0.010513, 0.0, 0.010513, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.021026, 0.0, 0.063078, 0.168207, 0.031539, 0.210259, 0.063078, 0.0, 0.094616, 0.0, 0.010513, 0.031539, 0.0, 0.010513, 0.0, 0.0, 0.031539, 0.0, 0.0, 0.0, 0.0, 0.052565, 0.105129, 0.0, 0.07359, 0.0, 0.0, 0.0, 0.021026, 0.042052, 0.0, 0.0, 0.105129, 0.084103, 0.168207, 0.0, 0.0, 0.0, 0.042052, 0.210259, 0.063078, 0.031539, 0.0, 0.052565, 0.0, 0.031539, 0.010513, 0.031539, 0.0, 0.021026, 0.031539, 0.042052, 0.010513]	2026-10-22 17:13:19.974202+00	2026-11-01 17:13:19.974202+00
\.


--
-- Data for Name: Attachment; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public."Attachment" ("AttachmentId", "TenantId", "ObjectType", "ObjectId", "UploadedBy", "FileName", "ContentType", "SizeBytes", "ObjectKey", "Hash", "Status", "CreatedAt", "DeletedAt") FROM stdin;
\.


--
-- Data for Name: AuditLog; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public."AuditLog" ("AuditLogId", "TenantId", "ActorType", "ActorId", "Action", "ObjectType", "ObjectId", "BeforeJson", "AfterJson", "MetadataJson", "CreatedAt") FROM stdin;
01a1981a-c1f4-7466-8100-d428362a89d6	019f8000-0000-7000-8000-000000000002	SYSTEM	\N	AUTH.LOGIN_SUCCEEDED	User	019f8000-0000-7000-8000-000000000010	\N	\N	{"familyId": "01a1981a-c1ce-7517-9cd3-59f9207a8323"}	2026-11-01 17:14:12.647079+00
01a1981d-efdc-7134-b86d-bb23d2ca62d5	019f8000-0000-7000-8000-000000000002	SYSTEM	\N	AUTH.LOGIN_SUCCEEDED	User	019f8000-0000-7000-8000-000000000010	\N	\N	{"familyId": "01a1981d-efdb-7118-b545-b49f47c1c160"}	2026-11-01 17:17:41.158247+00
01a1981f-8139-74f6-90ca-2f20752f2ebb	019f8000-0000-7000-8000-000000000002	SYSTEM	\N	AUTH.LOGIN_SUCCEEDED	User	019f8000-0000-7000-8000-000000000012	\N	\N	{"familyId": "01a1981f-8139-7a2e-95c2-007ddd6f5027"}	2026-11-01 17:19:23.888988+00
01a1981f-a815-7153-8893-f356cdfabd39	019f8000-0000-7000-8000-000000000002	SYSTEM	\N	AUTH.LOGIN_SUCCEEDED	User	019f8000-0000-7000-8000-000000000011	\N	\N	{"familyId": "01a1981f-a815-7a02-8059-c6077c214cb7"}	2026-11-01 17:19:33.85051+00
01a19821-1aa7-7f74-902c-1aa80e5fa462	019f8000-0000-7000-8000-000000000002	SYSTEM	\N	AUTH.LOGIN_SUCCEEDED	User	019f8000-0000-7000-8000-000000000010	\N	\N	{"familyId": "01a19821-1aa7-7779-b1f1-cc3e1e63adf5"}	2026-11-01 17:21:08.727733+00
01a19822-a814-74d8-b840-58e39ac22c16	019f8000-0000-7000-8000-000000000002	SYSTEM	\N	AUTH.LOGIN_SUCCEEDED	User	019f8000-0000-7000-8000-000000000012	\N	\N	{"familyId": "01a19822-a814-7d10-a82d-e5af610404ae"}	2026-11-01 17:22:50.450191+00
01a19822-cf7e-709b-b087-f88197e381ad	019f8000-0000-7000-8000-000000000002	SYSTEM	\N	AUTH.LOGIN_SUCCEEDED	User	019f8000-0000-7000-8000-000000000011	\N	\N	{"familyId": "01a19822-cf7e-7bc7-9bf9-67302d0b386d"}	2026-11-01 17:23:00.547031+00
01a19824-515e-7b30-b9a8-87531b9718a2	019f8000-0000-7000-8000-000000000002	SYSTEM	\N	AUTH.LOGIN_SUCCEEDED	User	019f8000-0000-7000-8000-000000000010	\N	\N	{"familyId": "01a19824-515e-7f75-852b-86b58e85762e"}	2026-11-01 17:24:39.337195+00
01a19825-1c89-7bec-aaf9-d3b1ae414fc6	019f8000-0000-7000-8000-000000000002	SYSTEM	\N	AUTH.LOGIN_SUCCEEDED	User	019f8000-0000-7000-8000-000000000012	\N	\N	{"familyId": "01a19825-1c89-7a9e-9c2a-ca32fc6017b8"}	2026-11-01 17:25:31.351335+00
01a19825-391a-712c-bb69-e5d0c13cd076	019f8000-0000-7000-8000-000000000002	SYSTEM	\N	AUTH.LOGIN_SUCCEEDED	User	019f8000-0000-7000-8000-000000000011	\N	\N	{"familyId": "01a19825-391a-741a-af4e-24505c4c7b0e"}	2026-11-01 17:25:38.651713+00
01a19826-6ab5-7a19-b348-015c3edeb5a0	019f8000-0000-7000-8000-000000000002	SYSTEM	\N	AUTH.LOGIN_SUCCEEDED	User	019f8000-0000-7000-8000-000000000010	\N	\N	{"familyId": "01a19826-6ab4-7830-b990-994e7f73fcf5"}	2026-11-01 17:26:56.899071+00
01a19827-71c6-75cf-b533-42cc9f7fe204	019f8000-0000-7000-8000-000000000002	SYSTEM	\N	AUTH.LOGIN_SUCCEEDED	User	019f8000-0000-7000-8000-000000000012	\N	\N	{"familyId": "01a19827-71c6-7241-b232-e6f995730c91"}	2026-11-01 17:28:04.243477+00
01a19827-9651-7627-8b4d-f7470919be60	019f8000-0000-7000-8000-000000000002	SYSTEM	\N	AUTH.LOGIN_SUCCEEDED	User	019f8000-0000-7000-8000-000000000011	\N	\N	{"familyId": "01a19827-9651-7311-a1d1-39f6b4cc2566"}	2026-11-01 17:28:13.597812+00
01a1982a-c523-7466-a9da-fcab68802690	019f8000-0000-7000-8000-000000000002	SYSTEM	\N	AUTH.LOGIN_SUCCEEDED	User	019f8000-0000-7000-8000-000000000010	\N	\N	{"familyId": "01a1982a-c523-7776-9743-e63102fb9b50"}	2026-11-01 17:31:42.19437+00
\.


--
-- Data for Name: AuthenticationSession; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public."AuthenticationSession" ("AuthenticationSessionId", "UserId", "FamilyId", "TokenHash", "CreatedAt", "ExpiresAt", "RevokedAt", "ReplacedById") FROM stdin;
01a1981a-c1ce-7517-9cd3-59f9207a8323	019f8000-0000-7000-8000-000000000010	01a1981a-c1ce-7517-9cd3-59f9207a8323	16422b98578c92c11166e2e9ed59d5af77a05238a0f46597b9c216e876a3f1d7	2026-11-01 17:14:12.647079+00	2026-11-15 17:14:12.647079+00	\N	\N
01a1981d-efdb-7118-b545-b49f47c1c160	019f8000-0000-7000-8000-000000000010	01a1981d-efdb-7118-b545-b49f47c1c160	f4d66ae9ed9e7f6b8da6644cdd837c712f2fda7732ac4ef895ff0beadb78f62c	2026-11-01 17:17:41.158247+00	2026-11-15 17:17:41.158247+00	\N	\N
01a1981f-8139-7a2e-95c2-007ddd6f5027	019f8000-0000-7000-8000-000000000012	01a1981f-8139-7a2e-95c2-007ddd6f5027	4cd785cd43f4dbb71cab8cfac95a6eb3fb11aa381cf6ce3b9b6100b796062aef	2026-11-01 17:19:23.888988+00	2026-11-15 17:19:23.888988+00	\N	\N
01a1981f-a815-7a02-8059-c6077c214cb7	019f8000-0000-7000-8000-000000000011	01a1981f-a815-7a02-8059-c6077c214cb7	a5fe5024474be3a98a86164a6a3bac0bca82d43fc01394d8b44644544931e2fe	2026-11-01 17:19:33.85051+00	2026-11-15 17:19:33.85051+00	\N	\N
01a19821-1aa7-7779-b1f1-cc3e1e63adf5	019f8000-0000-7000-8000-000000000010	01a19821-1aa7-7779-b1f1-cc3e1e63adf5	9a39611b61fedc2ae54362aa4e1b13391fb5397026c1fc727ea2bcca14ffdb33	2026-11-01 17:21:08.727733+00	2026-11-15 17:21:08.727733+00	\N	\N
01a19822-a814-7d10-a82d-e5af610404ae	019f8000-0000-7000-8000-000000000012	01a19822-a814-7d10-a82d-e5af610404ae	7b81185808b3b964dd3507522829676a9efb53e756c44a1ef080691ad311a3b6	2026-11-01 17:22:50.450191+00	2026-11-15 17:22:50.450191+00	\N	\N
01a19822-cf7e-7bc7-9bf9-67302d0b386d	019f8000-0000-7000-8000-000000000011	01a19822-cf7e-7bc7-9bf9-67302d0b386d	3a6f2d3d2e2173bdd821491c05c9f37a834b83210439c6764f7823d19dd3725f	2026-11-01 17:23:00.547031+00	2026-11-15 17:23:00.547031+00	\N	\N
01a19824-515e-7f75-852b-86b58e85762e	019f8000-0000-7000-8000-000000000010	01a19824-515e-7f75-852b-86b58e85762e	550e39f7760bd99dff0a05e4791076331587935588a6cca6e0c447a11857b2b5	2026-11-01 17:24:39.337195+00	2026-11-15 17:24:39.337195+00	\N	\N
01a19825-1c89-7a9e-9c2a-ca32fc6017b8	019f8000-0000-7000-8000-000000000012	01a19825-1c89-7a9e-9c2a-ca32fc6017b8	34d1e3ed30ec32120d58b898e20d4829d80d8f0b85243d38beca9b5854ac4622	2026-11-01 17:25:31.351335+00	2026-11-15 17:25:31.351335+00	\N	\N
01a19825-391a-741a-af4e-24505c4c7b0e	019f8000-0000-7000-8000-000000000011	01a19825-391a-741a-af4e-24505c4c7b0e	cf27dde4119f25eccdd21970d729d292b528d0671c7cb3c9ec3f51400efedf42	2026-11-01 17:25:38.651713+00	2026-11-15 17:25:38.651713+00	\N	\N
01a19826-6ab4-7830-b990-994e7f73fcf5	019f8000-0000-7000-8000-000000000010	01a19826-6ab4-7830-b990-994e7f73fcf5	18cf448ce85be1167b380cd3136a8d9140ba0fd5fdce2cb9d6243df4fd0f987a	2026-11-01 17:26:56.899071+00	2026-11-15 17:26:56.899071+00	\N	\N
01a19827-71c6-7241-b232-e6f995730c91	019f8000-0000-7000-8000-000000000012	01a19827-71c6-7241-b232-e6f995730c91	6beb1f844874eee67a9d34a76413cabc651fe70b372eb20a35b983c4f540bdfa	2026-11-01 17:28:04.243477+00	2026-11-15 17:28:04.243477+00	\N	\N
01a19827-9651-7311-a1d1-39f6b4cc2566	019f8000-0000-7000-8000-000000000011	01a19827-9651-7311-a1d1-39f6b4cc2566	4759a210ed020cacc6756b67c3fcef30f86c3c896a8aaf0b0054b1caed597f83	2026-11-01 17:28:13.597812+00	2026-11-15 17:28:13.597812+00	\N	\N
01a1982a-c523-7776-9743-e63102fb9b50	019f8000-0000-7000-8000-000000000010	01a1982a-c523-7776-9743-e63102fb9b50	26f5ee6c1930eca6e7396186f70a0d35c3a7f8541c341fba8aebdb5ebda75198	2026-11-01 17:31:42.19437+00	2026-11-15 17:31:42.19437+00	\N	\N
\.


--
-- Data for Name: BusinessCalendar; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public."BusinessCalendar" ("CalendarId", "TenantId", "TimeZone", "WorkingHoursJson", "HolidaysJson") FROM stdin;
\.


--
-- Data for Name: Comment; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public."Comment" ("CommentId", "TenantId", "ObjectType", "ObjectId", "AuthorId", "Content", "CreatedAt", "EditedAt", "DeletedAt") FROM stdin;
\.


--
-- Data for Name: Company; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public."Company" ("CompanyId", "Code", "Name", "ContactEmail", "Status", "CreatedAt", "UpdatedAt", "TaxId", "ContactPersonName", "ContactPhone", "EvidenceDocumentKey", "EvidenceDocumentFileName") FROM stdin;
019f8000-0000-7000-8000-000000000001	BIZFLOW	BizFlow Corporation	admin@bizflow.local	ACTIVE	2026-11-01 17:13:15.97076+00	2026-11-01 17:13:15.97076+00				\N	\N
\.


--
-- Data for Name: Confirmation; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public."Confirmation" ("ConfirmationId", "TenantId", "ObjectType", "ObjectId", "MilestoneType", "ActorId", "Decision", "Note", "ConfirmedAt") FROM stdin;
\.


--
-- Data for Name: DeadlineReminderSchedule; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public."DeadlineReminderSchedule" ("Id", "TenantId", "TaskId", "UserId", "LeadTimeMinutes", "RemindAt", "TriggerType", "Status", "CreatedAt") FROM stdin;
\.


--
-- Data for Name: Department; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public."Department" ("DepartmentId", "TenantId", "Code", "Name", "ParentDepartmentId", "Status", "CreatedAt", "UpdatedAt") FROM stdin;
019f8000-0000-7000-8000-000000000004	019f8000-0000-7000-8000-000000000002	EXEC	Ban Giám đốc & Điều hành	\N	ACTIVE	2026-11-01 17:13:19.873178+00	2026-11-01 17:13:19.873178+00
019f8000-0000-7000-8000-000000000003	019f8000-0000-7000-8000-000000000002	TECH	Phòng Công nghệ & Chuyển đổi số	019f8000-0000-7000-8000-000000000004	ACTIVE	2026-11-01 17:13:15.975485+00	2026-11-01 17:13:15.975485+00
019f8000-0000-7000-8000-000000000005	019f8000-0000-7000-8000-000000000002	SALES	Phòng Kinh doanh B2B & Đối tác	019f8000-0000-7000-8000-000000000004	ACTIVE	2026-11-01 17:13:19.877542+00	2026-11-01 17:13:19.877542+00
019f8000-0000-7000-8000-000000000006	019f8000-0000-7000-8000-000000000002	MKT	Phòng Tiếp thị & Truyền thông Thương hiệu	019f8000-0000-7000-8000-000000000004	ACTIVE	2026-11-01 17:13:19.878803+00	2026-11-01 17:13:19.878803+00
019f8000-0000-7000-8000-000000000007	019f8000-0000-7000-8000-000000000002	FIN	Phòng Tài chính - Kế toán	019f8000-0000-7000-8000-000000000004	ACTIVE	2026-11-01 17:13:19.879757+00	2026-11-01 17:13:19.879757+00
019f8000-0000-7000-8000-000000000008	019f8000-0000-7000-8000-000000000002	HR	Phòng Hành chính - Nhân sự	019f8000-0000-7000-8000-000000000004	ACTIVE	2026-11-01 17:13:19.88085+00	2026-11-01 17:13:19.88085+00
019f8000-0000-7000-8000-000000000009	019f8000-0000-7000-8000-000000000002	CS	Phòng Dịch vụ Khách hàng & Vận hành	019f8000-0000-7000-8000-000000000004	ACTIVE	2026-11-01 17:13:19.881956+00	2026-11-01 17:13:19.881956+00
\.


--
-- Data for Name: ManagementScope; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public."ManagementScope" ("ManagementScopeId", "TenantId", "UserId", "DepartmentId", "IncludeDescendants", "CreatedBy", "CreatedAt") FROM stdin;
019f8000-0000-7000-8000-000000000020	019f8000-0000-7000-8000-000000000002	019f8000-0000-7000-8000-000000000010	019f8000-0000-7000-8000-000000000003	t	019f8000-0000-7000-8000-000000000010	2026-11-01 17:13:15.991098+00
019f8000-0000-7000-8000-000000000021	019f8000-0000-7000-8000-000000000002	019f8000-0000-7000-8000-000000000012	019f8000-0000-7000-8000-000000000003	t	019f8000-0000-7000-8000-000000000010	2026-11-01 17:13:15.994794+00
019f8000-0000-7000-8000-000000000022	019f8000-0000-7000-8000-000000000002	019f8000-0000-7000-8000-000000000013	019f8000-0000-7000-8000-000000000004	t	019f8000-0000-7000-8000-000000000010	2026-11-01 17:13:19.911637+00
019f8000-0000-7000-8000-000000000023	019f8000-0000-7000-8000-000000000002	019f8000-0000-7000-8000-000000000014	019f8000-0000-7000-8000-000000000005	t	019f8000-0000-7000-8000-000000000010	2026-11-01 17:13:19.912818+00
019f8000-0000-7000-8000-000000000024	019f8000-0000-7000-8000-000000000002	019f8000-0000-7000-8000-000000000016	019f8000-0000-7000-8000-000000000006	t	019f8000-0000-7000-8000-000000000010	2026-11-01 17:13:19.912818+00
019f8000-0000-7000-8000-000000000025	019f8000-0000-7000-8000-000000000002	019f8000-0000-7000-8000-000000000018	019f8000-0000-7000-8000-000000000007	t	019f8000-0000-7000-8000-000000000010	2026-11-01 17:13:19.912818+00
019f8000-0000-7000-8000-000000000026	019f8000-0000-7000-8000-000000000002	019f8000-0000-7000-8000-00000000001a	019f8000-0000-7000-8000-000000000008	t	019f8000-0000-7000-8000-000000000010	2026-11-01 17:13:19.912818+00
019f8000-0000-7000-8000-000000000027	019f8000-0000-7000-8000-000000000002	019f8000-0000-7000-8000-00000000001c	019f8000-0000-7000-8000-000000000009	t	019f8000-0000-7000-8000-000000000010	2026-11-01 17:13:19.912818+00
\.


--
-- Data for Name: Notification; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public."Notification" ("NotificationId", "TenantId", "RecipientId", "Type", "ObjectType", "ObjectId", "Title", "Content", "ReadAt", "SentAt", "IdempotencyKey") FROM stdin;
\.


--
-- Data for Name: NotificationLog; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public."NotificationLog" ("Id", "TenantId", "TaskId", "UserId", "TriggerType", "ScheduledAt", "Channel", "Status", "DeliveredAt", "ErrorMessage") FROM stdin;
\.


--
-- Data for Name: Permission; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public."Permission" ("PermissionId", "Code", "Module", "Action", "ScopeType") FROM stdin;
019f7a64-0000-7000-8000-000000000001	platform.company-registrations.read	platform	company-registrations.read	PLATFORM
019f7f8a-0000-7000-8000-000000000001	roles.configure	roles	configure	TENANT
01a0f520-0000-7000-8000-000000000001	users.read	users	read	TENANT
01a0f590-0000-7000-8000-000000000001	workflows.read	workflows	read	TENANT
01a0f590-0000-7000-8000-000000000002	workflows.configure	workflows	configure	TENANT
01a0f620-0000-7000-8000-000000000001	audit.read	audit	read	TENANT
01a0f620-0000-7000-8000-000000000002	platform.audit.read	platform	audit.read	PLATFORM
01a10259-0000-7000-8000-000000000001	sla.read	sla	read	TENANT
01a10259-0000-7000-8000-000000000002	sla.configure	sla	configure	TENANT
01a10302-0000-7000-8000-000000000001	service.create	service	create	TENANT
01a10400-0000-7000-8000-000000000001	service.update	service	update	TENANT
01a10900-0000-7000-8000-000000000001	tasks.read.tenant	tasks	read	TENANT
01a10900-0000-7000-8000-000000000002	tasks.read.managed	tasks	read	DEPARTMENT
01a10900-0000-7000-8000-000000000003	tasks.read.own	tasks	read	SELF
01a10900-0000-7000-8000-000000000004	tasks.read.assigned	tasks	read	ASSIGNED
01a10a00-0000-7000-8000-000000000001	tasks.create	tasks	create	TENANT
01a11000-0000-7000-8000-000000000001	tasks.assign	tasks	assign	TENANT
01a11200-0000-7000-8000-000000000001	tasks.accept	tasks	accept	TENANT
01a11300-0000-7000-8000-000000000001	tasks.execute	tasks	execute	TENANT
01a11400-0000-7000-8000-000000000001	tasks.submit	tasks	submit	TENANT
01a11500-0000-7000-8000-000000000001	tasks.confirm	tasks	confirm	TENANT
01a12000-0000-7000-8000-000000000001	requests.create	requests	create	TENANT
01a12000-0000-7000-8000-000000000002	requests.submit	requests	submit	SELF
01a12000-0000-7000-8000-000000000003	requests.read.tenant	requests	read	TENANT
01a12000-0000-7000-8000-000000000004	requests.read.managed	requests	read	DEPARTMENT
01a12000-0000-7000-8000-000000000005	requests.read.own	requests	read	SELF
01a12000-0000-7000-8000-000000000006	requests.route	requests	route	TENANT
01a12000-0000-7000-8000-000000000007	requests.receive	requests	receive	TENANT
01a12000-0000-7000-8000-000000000008	requests.process	requests	process	TENANT
01a12000-0000-7000-8000-000000000009	requests.reject	requests	reject	TENANT
01a12000-0000-7000-8000-00000000000a	requests.cancel	requests	cancel	SELF
01a12000-0000-7000-8000-00000000000b	requests.resolve	requests	resolve	TENANT
01a12000-0000-7000-8000-00000000000c	requests.confirm	requests	confirm	TENANT
01a14000-0000-7000-8000-000000000001	comments.create	collaboration	create	TENANT
01a14000-0000-7000-8000-000000000002	comments.read	collaboration	read	TENANT
01a14000-0000-7000-8000-000000000003	attachments.upload	collaboration	upload	TENANT
01a14000-0000-7000-8000-000000000004	attachments.read	collaboration	read	TENANT
01a14000-0000-7000-8000-000000000005	attachments.delete	collaboration	delete	TENANT
01a14000-0000-7000-8000-000000000006	records.archive	collaboration	archive	TENANT
01a14000-0000-7000-8000-000000000007	records.search	collaboration	search	TENANT
01a14000-0000-7000-8000-000000000008	reports.manager	reports	manager	TENANT
01a14000-0000-7000-8000-000000000009	reports.company	reports	company	TENANT
01a14000-0000-7000-8000-00000000000a	reports.workload	reports	workload	TENANT
01a14000-0000-7000-8000-00000000000b	reports.sla	reports	sla	TENANT
01a14000-0000-7000-8000-000000000010	ai.001	ai	assist_task	TENANT
01a14000-0000-7000-8000-000000000011	ai.002	ai	recommend_assignment	TENANT
01a14000-0000-7000-8000-000000000012	ai.003	ai	recommend_parameters	TENANT
01a14000-0000-7000-8000-000000000013	ai.004	ai	task_breakdown	TENANT
01a14000-0000-7000-8000-000000000014	ai.005	ai	task_risk	TENANT
01a14000-0000-7000-8000-000000000015	ai.006	ai	task_summary	TENANT
01a14000-0000-7000-8000-000000000016	ai.007	ai	request_multi_intent	TENANT
01a14000-0000-7000-8000-000000000017	ai.008	ai	request_split	TENANT
01a14000-0000-7000-8000-000000000018	ai.009	ai	request_routing	TENANT
01a14000-0000-7000-8000-000000000019	ai.010	ai	action_execute	TENANT
\.


--
-- Data for Name: Request; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public."Request" ("RequestId", "TenantId", "RequesterId", "ServiceId", "CategoryId", "ParentRequestId", "RevisedFromRequestId", "Title", "Description", "Priority", "Status", "WorkflowVersionId", "SLAVersionId", "ResolvedAt", "ClosedAt", "CreatedAt", "UpdatedAt", "DeletedAt") FROM stdin;
019f8000-0000-7000-8000-000000000050	019f8000-0000-7000-8000-000000000002	019f8000-0000-7000-8000-00000000001b	019f8000-0000-7000-8000-000000000032	019f8000-0000-7000-8000-000000000033	\N	\N	Yêu cầu cấp phát laptop cấu hình cao cho Kỹ sư Full-Stack mới	Yêu cầu cấp phát 01 laptop Dell XPS 15 (32GB RAM, 1TB SSD) cho Kỹ sư Full-stack chuẩn bị gia nhập công ty vào thứ Hai tuần tới.	HIGH	IN_PROGRESS	\N	\N	\N	\N	2026-10-30 17:13:19.927172+00	2026-11-01 17:13:19.927172+00	\N
019f8000-0000-7000-8000-000000000051	019f8000-0000-7000-8000-000000000002	019f8000-0000-7000-8000-000000000015	019f8000-0000-7000-8000-000000000035	019f8000-0000-7000-8000-000000000036	\N	\N	Tạm ứng công tác phí hội nghị Chuyển đổi số Quốc gia tại Đà Nẵng	Đề nghị tạm ứng số tiền 15.000.000 VNĐ phục vụ chuyến công tác 3 ngày 2 đêm gặp gỡ đối tác và đại diện gian hàng triển lãm BizFlow.	MEDIUM	SUBMITTED	\N	\N	\N	\N	2026-10-31 17:13:19.931674+00	2026-11-01 17:13:19.931674+00	\N
019f8000-0000-7000-8000-000000000052	019f8000-0000-7000-8000-000000000002	019f8000-0000-7000-8000-00000000001a	019f8000-0000-7000-8000-000000000038	019f8000-0000-7000-8000-000000000039	\N	\N	Thiết kế bộ banner và standee cho sự kiện Kick-off Q4	Cần thiết kế bộ ấn phẩm gồm 01 backdrop sân khấu chính (6m x 3m) và 02 standee đứng chào đón nhân viên tại sảnh chính tòa nhà.	MEDIUM	RESOLVED	\N	\N	2026-11-01 14:13:19.932917+00	\N	2026-10-29 17:13:19.932917+00	2026-11-01 17:13:19.932917+00	\N
\.


--
-- Data for Name: RequestResolution; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public."RequestResolution" ("ResolutionId", "RequestId", "ResolverId", "Content", "RevisionNo", "CreatedAt") FROM stdin;
\.


--
-- Data for Name: RequestRouting; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public."RequestRouting" ("RequestRoutingId", "TenantId", "RequestId", "FromDepartmentId", "ToDepartmentId", "FromUserId", "ToUserId", "RoutedBy", "RoutedAt", "Reason", "Source") FROM stdin;
\.


--
-- Data for Name: Role; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public."Role" ("RoleId", "TenantId", "Name", "IsSystem", "Status", "CreatedAt", "UpdatedAt") FROM stdin;
019f7a64-0000-7000-8000-000000000002	\N	PLATFORM_ADMIN	t	ACTIVE	2026-09-30 00:00:00+00	2026-09-30 00:00:00+00
019f7f8a-0000-7000-8000-000000000002	\N	COMPANY_ADMIN	t	ACTIVE	2026-10-01 00:00:00+00	2026-10-01 00:00:00+00
019f7f8a-0000-7000-8000-000000000003	\N	MANAGER	t	ACTIVE	2026-10-01 00:00:00+00	2026-10-01 00:00:00+00
019f7f8a-0000-7000-8000-000000000004	\N	EMPLOYEE	t	ACTIVE	2026-10-01 00:00:00+00	2026-10-01 00:00:00+00
\.


--
-- Data for Name: RolePermission; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public."RolePermission" ("RoleId", "PermissionId") FROM stdin;
019f7a64-0000-7000-8000-000000000002	019f7a64-0000-7000-8000-000000000001
019f7f8a-0000-7000-8000-000000000002	019f7f8a-0000-7000-8000-000000000001
019f7f8a-0000-7000-8000-000000000002	01a0f520-0000-7000-8000-000000000001
019f7f8a-0000-7000-8000-000000000003	01a0f520-0000-7000-8000-000000000001
019f7f8a-0000-7000-8000-000000000004	01a0f520-0000-7000-8000-000000000001
019f7f8a-0000-7000-8000-000000000002	01a0f590-0000-7000-8000-000000000001
019f7f8a-0000-7000-8000-000000000003	01a0f590-0000-7000-8000-000000000001
019f7f8a-0000-7000-8000-000000000004	01a0f590-0000-7000-8000-000000000001
019f7f8a-0000-7000-8000-000000000002	01a0f590-0000-7000-8000-000000000002
019f7f8a-0000-7000-8000-000000000002	01a0f620-0000-7000-8000-000000000001
019f7a64-0000-7000-8000-000000000002	01a0f620-0000-7000-8000-000000000002
019f7f8a-0000-7000-8000-000000000002	01a10259-0000-7000-8000-000000000001
019f7f8a-0000-7000-8000-000000000002	01a10259-0000-7000-8000-000000000002
019f7f8a-0000-7000-8000-000000000002	01a10302-0000-7000-8000-000000000001
019f7f8a-0000-7000-8000-000000000002	01a10400-0000-7000-8000-000000000001
019f7f8a-0000-7000-8000-000000000002	01a10900-0000-7000-8000-000000000001
019f7f8a-0000-7000-8000-000000000003	01a10900-0000-7000-8000-000000000002
019f7f8a-0000-7000-8000-000000000003	01a10900-0000-7000-8000-000000000003
019f7f8a-0000-7000-8000-000000000003	01a10900-0000-7000-8000-000000000004
019f7f8a-0000-7000-8000-000000000004	01a10900-0000-7000-8000-000000000003
019f7f8a-0000-7000-8000-000000000004	01a10900-0000-7000-8000-000000000004
019f7f8a-0000-7000-8000-000000000003	01a10a00-0000-7000-8000-000000000001
019f7f8a-0000-7000-8000-000000000003	01a11000-0000-7000-8000-000000000001
019f7f8a-0000-7000-8000-000000000003	01a11200-0000-7000-8000-000000000001
019f7f8a-0000-7000-8000-000000000004	01a11200-0000-7000-8000-000000000001
019f7f8a-0000-7000-8000-000000000003	01a11300-0000-7000-8000-000000000001
019f7f8a-0000-7000-8000-000000000004	01a11300-0000-7000-8000-000000000001
019f7f8a-0000-7000-8000-000000000003	01a11400-0000-7000-8000-000000000001
019f7f8a-0000-7000-8000-000000000004	01a11400-0000-7000-8000-000000000001
019f7f8a-0000-7000-8000-000000000002	01a11500-0000-7000-8000-000000000001
019f7f8a-0000-7000-8000-000000000003	01a11500-0000-7000-8000-000000000001
019f7f8a-0000-7000-8000-000000000002	01a12000-0000-7000-8000-000000000001
019f7f8a-0000-7000-8000-000000000002	01a12000-0000-7000-8000-000000000002
019f7f8a-0000-7000-8000-000000000002	01a12000-0000-7000-8000-000000000003
019f7f8a-0000-7000-8000-000000000002	01a12000-0000-7000-8000-000000000005
019f7f8a-0000-7000-8000-000000000003	01a12000-0000-7000-8000-000000000001
019f7f8a-0000-7000-8000-000000000003	01a12000-0000-7000-8000-000000000002
019f7f8a-0000-7000-8000-000000000003	01a12000-0000-7000-8000-000000000004
019f7f8a-0000-7000-8000-000000000003	01a12000-0000-7000-8000-000000000005
019f7f8a-0000-7000-8000-000000000004	01a12000-0000-7000-8000-000000000001
019f7f8a-0000-7000-8000-000000000004	01a12000-0000-7000-8000-000000000002
019f7f8a-0000-7000-8000-000000000004	01a12000-0000-7000-8000-000000000005
019f7f8a-0000-7000-8000-000000000002	01a12000-0000-7000-8000-000000000006
019f7f8a-0000-7000-8000-000000000002	01a12000-0000-7000-8000-000000000007
019f7f8a-0000-7000-8000-000000000002	01a12000-0000-7000-8000-000000000008
019f7f8a-0000-7000-8000-000000000002	01a12000-0000-7000-8000-000000000009
019f7f8a-0000-7000-8000-000000000002	01a12000-0000-7000-8000-00000000000a
019f7f8a-0000-7000-8000-000000000003	01a12000-0000-7000-8000-000000000006
019f7f8a-0000-7000-8000-000000000003	01a12000-0000-7000-8000-000000000007
019f7f8a-0000-7000-8000-000000000003	01a12000-0000-7000-8000-000000000008
019f7f8a-0000-7000-8000-000000000003	01a12000-0000-7000-8000-000000000009
019f7f8a-0000-7000-8000-000000000003	01a12000-0000-7000-8000-00000000000a
019f7f8a-0000-7000-8000-000000000004	01a12000-0000-7000-8000-000000000007
019f7f8a-0000-7000-8000-000000000004	01a12000-0000-7000-8000-000000000008
019f7f8a-0000-7000-8000-000000000004	01a12000-0000-7000-8000-00000000000a
019f7f8a-0000-7000-8000-000000000002	01a12000-0000-7000-8000-00000000000b
019f7f8a-0000-7000-8000-000000000002	01a12000-0000-7000-8000-00000000000c
019f7f8a-0000-7000-8000-000000000003	01a12000-0000-7000-8000-00000000000b
019f7f8a-0000-7000-8000-000000000003	01a12000-0000-7000-8000-00000000000c
019f7f8a-0000-7000-8000-000000000004	01a12000-0000-7000-8000-00000000000b
019f7f8a-0000-7000-8000-000000000004	01a12000-0000-7000-8000-00000000000c
019f7f8a-0000-7000-8000-000000000002	01a14000-0000-7000-8000-000000000001
019f7f8a-0000-7000-8000-000000000002	01a14000-0000-7000-8000-000000000002
019f7f8a-0000-7000-8000-000000000003	01a14000-0000-7000-8000-000000000001
019f7f8a-0000-7000-8000-000000000003	01a14000-0000-7000-8000-000000000002
019f7f8a-0000-7000-8000-000000000004	01a14000-0000-7000-8000-000000000001
019f7f8a-0000-7000-8000-000000000004	01a14000-0000-7000-8000-000000000002
019f7f8a-0000-7000-8000-000000000002	01a14000-0000-7000-8000-000000000003
019f7f8a-0000-7000-8000-000000000002	01a14000-0000-7000-8000-000000000004
019f7f8a-0000-7000-8000-000000000002	01a14000-0000-7000-8000-000000000005
019f7f8a-0000-7000-8000-000000000003	01a14000-0000-7000-8000-000000000003
019f7f8a-0000-7000-8000-000000000003	01a14000-0000-7000-8000-000000000004
019f7f8a-0000-7000-8000-000000000003	01a14000-0000-7000-8000-000000000005
019f7f8a-0000-7000-8000-000000000004	01a14000-0000-7000-8000-000000000003
019f7f8a-0000-7000-8000-000000000004	01a14000-0000-7000-8000-000000000004
019f7f8a-0000-7000-8000-000000000004	01a14000-0000-7000-8000-000000000005
019f7f8a-0000-7000-8000-000000000002	01a14000-0000-7000-8000-000000000006
019f7f8a-0000-7000-8000-000000000002	01a14000-0000-7000-8000-000000000007
019f7f8a-0000-7000-8000-000000000003	01a14000-0000-7000-8000-000000000006
019f7f8a-0000-7000-8000-000000000003	01a14000-0000-7000-8000-000000000007
019f7f8a-0000-7000-8000-000000000004	01a14000-0000-7000-8000-000000000007
019f7f8a-0000-7000-8000-000000000002	01a14000-0000-7000-8000-000000000008
019f7f8a-0000-7000-8000-000000000002	01a14000-0000-7000-8000-000000000009
019f7f8a-0000-7000-8000-000000000002	01a14000-0000-7000-8000-00000000000a
019f7f8a-0000-7000-8000-000000000002	01a14000-0000-7000-8000-00000000000b
019f7f8a-0000-7000-8000-000000000003	01a14000-0000-7000-8000-000000000008
019f7f8a-0000-7000-8000-000000000003	01a14000-0000-7000-8000-00000000000a
019f7f8a-0000-7000-8000-000000000003	01a14000-0000-7000-8000-00000000000b
019f7f8a-0000-7000-8000-000000000002	01a14000-0000-7000-8000-000000000010
019f7f8a-0000-7000-8000-000000000002	01a14000-0000-7000-8000-000000000011
019f7f8a-0000-7000-8000-000000000002	01a14000-0000-7000-8000-000000000012
019f7f8a-0000-7000-8000-000000000002	01a14000-0000-7000-8000-000000000013
019f7f8a-0000-7000-8000-000000000002	01a14000-0000-7000-8000-000000000014
019f7f8a-0000-7000-8000-000000000002	01a14000-0000-7000-8000-000000000015
019f7f8a-0000-7000-8000-000000000002	01a14000-0000-7000-8000-000000000016
019f7f8a-0000-7000-8000-000000000002	01a14000-0000-7000-8000-000000000017
019f7f8a-0000-7000-8000-000000000002	01a14000-0000-7000-8000-000000000018
019f7f8a-0000-7000-8000-000000000002	01a14000-0000-7000-8000-000000000019
019f7f8a-0000-7000-8000-000000000003	01a14000-0000-7000-8000-000000000010
019f7f8a-0000-7000-8000-000000000003	01a14000-0000-7000-8000-000000000011
019f7f8a-0000-7000-8000-000000000003	01a14000-0000-7000-8000-000000000012
019f7f8a-0000-7000-8000-000000000003	01a14000-0000-7000-8000-000000000013
019f7f8a-0000-7000-8000-000000000003	01a14000-0000-7000-8000-000000000014
019f7f8a-0000-7000-8000-000000000003	01a14000-0000-7000-8000-000000000015
019f7f8a-0000-7000-8000-000000000003	01a14000-0000-7000-8000-000000000016
019f7f8a-0000-7000-8000-000000000003	01a14000-0000-7000-8000-000000000017
019f7f8a-0000-7000-8000-000000000003	01a14000-0000-7000-8000-000000000018
019f7f8a-0000-7000-8000-000000000003	01a14000-0000-7000-8000-000000000019
019f7f8a-0000-7000-8000-000000000004	01a14000-0000-7000-8000-000000000015
019f7f8a-0000-7000-8000-000000000004	01a14000-0000-7000-8000-000000000016
019f7f8a-0000-7000-8000-000000000004	01a14000-0000-7000-8000-000000000017
019f7f8a-0000-7000-8000-000000000004	01a14000-0000-7000-8000-000000000018
019f7f8a-0000-7000-8000-000000000004	01a14000-0000-7000-8000-000000000019
019f7f8a-0000-7000-8000-000000000004	01a10a00-0000-7000-8000-000000000001
\.


--
-- Data for Name: SLAProfile; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public."SLAProfile" ("SLAProfileId", "TenantId", "Name", "Status") FROM stdin;
\.


--
-- Data for Name: SLAVersion; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public."SLAVersion" ("SLAVersionId", "SLAProfileId", "VersionNo", "TargetMinutes", "WarningMinutes", "CalendarId", "EscalationConfigJson") FROM stdin;
\.


--
-- Data for Name: Service; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public."Service" ("ServiceId", "TenantId", "Code", "Name", "Description", "Status", "ActiveWorkflowVersionId", "ActiveSLAVersionId", "CreatedAt", "UpdatedAt") FROM stdin;
019f8000-0000-7000-8000-000000000030	019f8000-0000-7000-8000-000000000002	IT-SUPPORT	Hỗ trợ Kỹ thuật & Hạ tầng CNTT	Dịch vụ tiếp nhận và xử lý sự cố CNTT nội bộ doanh nghiệp	ACTIVE	\N	\N	2026-11-01 17:13:15.997022+00	2026-11-01 17:13:15.997022+00
019f8000-0000-7000-8000-000000000032	019f8000-0000-7000-8000-000000000002	HR-PORTAL	Dịch vụ Hành chính - Nhân sự (HR Portal)	Cổng tiếp nhận đề xuất nhân sự, phúc lợi và trang thiết bị	ACTIVE	\N	\N	2026-11-01 17:13:19.920402+00	2026-11-01 17:13:19.920402+00
019f8000-0000-7000-8000-000000000035	019f8000-0000-7000-8000-000000000002	FIN-EXPENSE	Dịch vụ Tài chính - Kế toán & Mua sắm	Tiếp nhận tạm ứng công tác phí và đề xuất mua sắm tài sản doanh nghiệp	ACTIVE	\N	\N	2026-11-01 17:13:19.922491+00	2026-11-01 17:13:19.922491+00
019f8000-0000-7000-8000-000000000038	019f8000-0000-7000-8000-000000000002	MKT-DESIGN	Dịch vụ Truyền thông & Thiết kế Tiếp thị	Yêu cầu ấn phẩm thiết kế, banner sự kiện và truyền thông báo chí	ACTIVE	\N	\N	2026-11-01 17:13:19.924443+00	2026-11-01 17:13:19.924443+00
\.


--
-- Data for Name: ServiceCategory; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public."ServiceCategory" ("ServiceCategoryId", "ServiceId", "Code", "Name", "Status") FROM stdin;
019f8000-0000-7000-8000-000000000031	019f8000-0000-7000-8000-000000000030	HARDWARE	Sự cố thiết bị phần cứng & máy trạm	ACTIVE
019f8000-0000-7000-8000-00000000003b	019f8000-0000-7000-8000-000000000030	SOFTWARE	Cài đặt phần mềm, cấp quyền tài khoản hệ thống	ACTIVE
019f8000-0000-7000-8000-000000000033	019f8000-0000-7000-8000-000000000032	ONBOARDING	Cấp phát trang thiết bị & Onboarding nhân sự mới	ACTIVE
019f8000-0000-7000-8000-000000000034	019f8000-0000-7000-8000-000000000032	LEAVE_REQ	Nghỉ phép, chế độ bảo hiểm và phúc lợi lao động	ACTIVE
019f8000-0000-7000-8000-000000000036	019f8000-0000-7000-8000-000000000035	TRAVEL_ADVANCE	Tạm ứng & Quyết toán công tác phí	ACTIVE
019f8000-0000-7000-8000-000000000037	019f8000-0000-7000-8000-000000000035	PROCUREMENT	Đề xuất mua sắm công cụ, vật tư và bản quyền phần mềm	ACTIVE
019f8000-0000-7000-8000-000000000039	019f8000-0000-7000-8000-000000000038	BRAND_ASSET	Yêu cầu ấn phẩm thiết kế, banner sự kiện	ACTIVE
019f8000-0000-7000-8000-00000000003a	019f8000-0000-7000-8000-000000000038	PRESS_RELEASE	Soạn thảo & Đăng tải thông cáo báo chí, tin tức	ACTIVE
\.


--
-- Data for Name: Task; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public."Task" ("TaskId", "TenantId", "RequestId", "CreatorId", "Title", "Description", "Priority", "Deadline", "Status", "WorkflowVersionId", "SLAVersionId", "CompletedAt", "CreatedAt", "UpdatedAt", "DeletedAt") FROM stdin;
019f8000-0000-7000-8000-000000000040	019f8000-0000-7000-8000-000000000002	\N	019f8000-0000-7000-8000-000000000010	Triển khai hạ tầng và thử nghiệm tính năng BizFlow AI	Thử nghiệm các tính năng AI cốt lõi: Phân rã nhiệm vụ (Task Breakdown), Đánh giá rủi ro (Risk Assessment), Gợi ý nhân sự tối ưu (Assignment Recommendation) và Tóm tắt công việc (Summary).	HIGH	2026-11-08 17:13:16.004781+00	IN_PROGRESS	\N	\N	\N	2026-11-01 17:13:16.004781+00	2026-11-01 17:13:16.004781+00	\N
019f8000-0000-7000-8000-000000000042	019f8000-0000-7000-8000-000000000002	\N	019f8000-0000-7000-8000-000000000013	Đàm phán hợp đồng cung cấp giải pháp chuyển đổi số với Tập đoàn ABC	Hoàn thiện báo giá chi tiết, kế hoạch triển khai giai đoạn 1 và thống nhất điều khoản bảo mật dữ liệu khách hàng với ban Pháp chế Tập đoàn ABC.	HIGH	2026-11-08 17:13:19.93482+00	IN_PROGRESS	\N	\N	\N	2026-10-29 17:13:19.93482+00	2026-11-01 17:13:19.93482+00	\N
019f8000-0000-7000-8000-000000000044	019f8000-0000-7000-8000-000000000002	\N	019f8000-0000-7000-8000-000000000010	Tuyển dụng 03 Kỹ sư Full-Stack Senior và Onboarding đợt 2 tháng 11	Tiến hành sàn lọc hồ sơ ứng viên, điều phối phỏng vấn kỹ thuật cùng phòng Công nghệ và chuẩn bị hợp đồng lao động thử việc.	MEDIUM	2026-11-06 17:13:19.93482+00	IN_PROGRESS	\N	\N	\N	2026-10-28 17:13:19.93482+00	2026-11-01 17:13:19.93482+00	\N
019f8000-0000-7000-8000-000000000045	019f8000-0000-7000-8000-000000000002	\N	019f8000-0000-7000-8000-000000000013	Thiết lập quy trình chăm sóc khách hàng VIP và khảo sát CSAT kỳ 2	Tổng hợp danh sách 50 khách hàng doanh nghiệp chiến lược, gửi thư tri ân và tiến hành khảo sát mức độ hài lòng về chất lượng hỗ trợ.	MEDIUM	2026-10-31 17:13:19.93482+00	CONFIRMED	\N	\N	2026-10-31 17:13:19.964656+00	2026-10-25 17:13:19.93482+00	2026-10-31 17:13:19.93482+00	\N
019f8000-0000-7000-8000-000000000041	019f8000-0000-7000-8000-000000000002	\N	019f8000-0000-7000-8000-000000000010	Tổ chức chiến dịch ra mắt sản phẩm Q4/2026 - BizFlow Enterprise	Chuẩn bị toàn diện tài liệu truyền thông, bộ nhận diện chiến dịch và landing page bán hàng cho giải pháp quản trị luồng công việc doanh nghiệp.	CRITICAL	2026-11-02 17:13:19.93482+00	ASSIGNED	\N	\N	\N	2026-10-31 17:13:19.93482+00	2026-11-01 17:13:19.93482+00	\N
019f8000-0000-7000-8000-000000000043	019f8000-0000-7000-8000-000000000002	\N	019f8000-0000-7000-8000-000000000010	Quyết toán báo cáo tài chính quý 3 và lập kế hoạch ngân sách năm 2027	Đối soát số dư công nợ khách hàng, kiểm toán chi phí vận hành các phòng ban và dự thảo hạn mức chi tiêu hoạt động cho năm tài chính 2027.	HIGH	2026-11-11 17:13:19.93482+00	ASSIGNED	\N	\N	\N	2026-10-30 17:13:19.93482+00	2026-11-01 17:13:19.93482+00	\N
\.


--
-- Data for Name: TaskAssignment; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public."TaskAssignment" ("TaskAssignmentId", "TaskId", "DepartmentId", "UserId", "AssignedBy", "AssignedAt", "AcceptedAt", "RejectedAt", "RejectionReason", "EndedAt") FROM stdin;
019f8000-0000-7000-8000-000000000061	019f8000-0000-7000-8000-000000000041	019f8000-0000-7000-8000-000000000006	019f8000-0000-7000-8000-000000000016	019f8000-0000-7000-8000-000000000010	2026-10-31 17:13:19.948837+00	\N	\N	\N	\N
019f8000-0000-7000-8000-000000000063	019f8000-0000-7000-8000-000000000043	019f8000-0000-7000-8000-000000000007	019f8000-0000-7000-8000-000000000019	019f8000-0000-7000-8000-000000000010	2026-10-30 17:13:19.948837+00	\N	\N	\N	\N
019f8000-0000-7000-8000-000000000060	019f8000-0000-7000-8000-000000000040	019f8000-0000-7000-8000-000000000003	019f8000-0000-7000-8000-000000000012	019f8000-0000-7000-8000-000000000010	2026-10-30 17:13:19.948837+00	2026-10-30 17:13:19.955833+00	\N	\N	\N
019f8000-0000-7000-8000-000000000062	019f8000-0000-7000-8000-000000000042	019f8000-0000-7000-8000-000000000005	019f8000-0000-7000-8000-000000000015	019f8000-0000-7000-8000-000000000013	2026-10-29 17:13:19.948837+00	2026-10-29 17:13:19.958129+00	\N	\N	\N
019f8000-0000-7000-8000-000000000064	019f8000-0000-7000-8000-000000000044	019f8000-0000-7000-8000-000000000008	019f8000-0000-7000-8000-00000000001b	019f8000-0000-7000-8000-000000000010	2026-10-28 17:13:19.948837+00	2026-10-28 17:13:19.959913+00	\N	\N	\N
019f8000-0000-7000-8000-000000000065	019f8000-0000-7000-8000-000000000045	019f8000-0000-7000-8000-000000000009	019f8000-0000-7000-8000-00000000001d	019f8000-0000-7000-8000-000000000013	2026-10-25 17:13:19.948837+00	2026-10-25 17:13:19.961619+00	\N	\N	\N
\.


--
-- Data for Name: TaskChecklistItem; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public."TaskChecklistItem" ("ChecklistItemId", "TaskId", "Title", "SortOrder", "IsCompleted", "CompletedBy", "CompletedAt") FROM stdin;
019f8000-0000-7000-8000-000000000072	019f8000-0000-7000-8000-000000000040	Đánh giá hiệu năng Vector Memory và độ trễ phản hồi	3	f	\N	\N
019f8000-0000-7000-8000-000000000073	019f8000-0000-7000-8000-000000000041	Hoàn thiện Landing Page và video demo sản phẩm	1	f	\N	\N
019f8000-0000-7000-8000-000000000074	019f8000-0000-7000-8000-000000000041	Gửi thông cáo báo chí đến các cơ quan truyền thông	2	f	\N	\N
019f8000-0000-7000-8000-000000000075	019f8000-0000-7000-8000-000000000041	Chuẩn bị tài liệu bán hàng (Sales Kit) cho bộ phận Kinh doanh	3	f	\N	\N
019f8000-0000-7000-8000-000000000070	019f8000-0000-7000-8000-000000000040	Thiết lập cấu hình PostgreSQL & pgvector schema	1	t	019f8000-0000-7000-8000-000000000012	2026-10-31 17:13:19.965735+00
019f8000-0000-7000-8000-000000000071	019f8000-0000-7000-8000-000000000040	Triển khai kiểm thử giao diện Trợ lý Robot AI	2	t	019f8000-0000-7000-8000-000000000011	2026-11-01 05:13:19.96717+00
019f8000-0000-7000-8000-000000000076	019f8000-0000-7000-8000-000000000045	Phân loại danh sách 50 khách hàng doanh nghiệp VIP	1	t	019f8000-0000-7000-8000-00000000001d	2026-10-30 17:13:19.969014+00
019f8000-0000-7000-8000-000000000077	019f8000-0000-7000-8000-000000000045	Gọi điện thăm hỏi và gửi phiếu khảo sát mức độ hài lòng	2	t	019f8000-0000-7000-8000-00000000001d	2026-10-31 17:13:19.970719+00
019f8000-0000-7000-8000-000000000078	019f8000-0000-7000-8000-000000000045	Tổng hợp báo cáo chỉ số CSAT và đề xuất cải tiến dịch vụ	3	t	019f8000-0000-7000-8000-00000000001d	2026-10-31 17:13:19.97225+00
\.


--
-- Data for Name: TaskProgressReport; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public."TaskProgressReport" ("ProgressReportId", "TaskId", "AuthorId", "Percent", "Content", "SubmittedAt") FROM stdin;
\.


--
-- Data for Name: TaskResult; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public."TaskResult" ("TaskResultId", "TaskId", "AuthorId", "Content", "RevisionNo", "SubmittedAt") FROM stdin;
\.


--
-- Data for Name: Tenant; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public."Tenant" ("TenantId", "CompanyId", "TenantKey", "Name", "Status", "TimeZone", "CreatedAt", "UpdatedAt") FROM stdin;
019f8000-0000-7000-8000-000000000002	019f8000-0000-7000-8000-000000000001	bizflow-corp	BizFlow Corporation	ACTIVE	Asia/Ho_Chi_Minh	2026-11-01 17:13:15.973145+00	2026-11-01 17:13:15.973145+00
\.


--
-- Data for Name: TenantSetting; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public."TenantSetting" ("TenantSettingId", "TenantId", "Key", "ValueJson", "UpdatedBy", "UpdatedAt") FROM stdin;
\.


--
-- Data for Name: User; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public."User" ("UserId", "TenantId", "IsPlatformAdministrator", "EmployeeCode", "NormalizedEmployeeCode", "Email", "NormalizedEmail", "PasswordHash", "SecurityStamp", "FullName", "DepartmentId", "Status", "AccessFailedCount", "LockoutEnd", "MustChangePassword", "LastLoginAt", "CreatedAt", "UpdatedAt", "DeletedAt") FROM stdin;
019f8000-0000-7000-8000-000000000013	019f8000-0000-7000-8000-000000000002	f	EXEC001	EXEC001	huong.nguyen@bizflow.local	HUONG.NGUYEN@BIZFLOW.LOCAL	AQAAAAIAAYagAAAAEMa9qd78Fhm+i6WlQIWbA0oTRemrrwoIP/XwOmaoEFH4y54oHQg5favZTIwG7ixFcQ==	STAMP_EXEC001	Nguyễn Thị Mai Hương (COO)	019f8000-0000-7000-8000-000000000004	ACTIVE	0	\N	f	\N	2026-11-01 17:13:19.885535+00	2026-11-01 17:13:19.885535+00	\N
019f8000-0000-7000-8000-000000000010	019f8000-0000-7000-8000-000000000002	f	ADMIN001	ADMIN001	admin@bizflow.local	ADMIN@BIZFLOW.LOCAL	AQAAAAIAAYagAAAAEMa9qd78Fhm+i6WlQIWbA0oTRemrrwoIP/XwOmaoEFH4y54oHQg5favZTIwG7ixFcQ==	019f8000000070008000000000000010	Phạm Minh Đức (CEO)	019f8000-0000-7000-8000-000000000004	ACTIVE	0	\N	f	2026-11-01 17:31:42.19437+00	2026-11-01 17:13:15.977357+00	2026-11-01 17:31:42.19437+00	\N
019f8000-0000-7000-8000-00000000001e	019f8000-0000-7000-8000-000000000002	f	TECH002	TECH002	huy.do@bizflow.local	HUY.DO@BIZFLOW.LOCAL	AQAAAAIAAYagAAAAEMa9qd78Fhm+i6WlQIWbA0oTRemrrwoIP/XwOmaoEFH4y54oHQg5favZTIwG7ixFcQ==	STAMP_TECH002	Đỗ Quang Huy (DevOps)	019f8000-0000-7000-8000-000000000003	ACTIVE	0	\N	f	\N	2026-11-01 17:13:19.887519+00	2026-11-01 17:13:19.887519+00	\N
019f8000-0000-7000-8000-000000000014	019f8000-0000-7000-8000-000000000002	f	SALES_MGR	SALES_MGR	dat.nguyen@bizflow.local	DAT.NGUYEN@BIZFLOW.LOCAL	AQAAAAIAAYagAAAAEMa9qd78Fhm+i6WlQIWbA0oTRemrrwoIP/XwOmaoEFH4y54oHQg5favZTIwG7ixFcQ==	STAMP_SALES_MGR	Nguyễn Tiến Đạt (Trưởng phòng Sales)	019f8000-0000-7000-8000-000000000005	ACTIVE	0	\N	f	\N	2026-11-01 17:13:19.889187+00	2026-11-01 17:13:19.889187+00	\N
019f8000-0000-7000-8000-000000000015	019f8000-0000-7000-8000-000000000002	f	SALES001	SALES001	kiet.hoang@bizflow.local	KIET.HOANG@BIZFLOW.LOCAL	AQAAAAIAAYagAAAAEMa9qd78Fhm+i6WlQIWbA0oTRemrrwoIP/XwOmaoEFH4y54oHQg5favZTIwG7ixFcQ==	STAMP_SALES001	Hoàng Tuấn Kiệt (Chuyên viên B2B)	019f8000-0000-7000-8000-000000000005	ACTIVE	0	\N	f	\N	2026-11-01 17:13:19.890178+00	2026-11-01 17:13:19.890178+00	\N
019f8000-0000-7000-8000-000000000016	019f8000-0000-7000-8000-000000000002	f	MKT_MGR	MKT_MGR	thao.vu@bizflow.local	THAO.VU@BIZFLOW.LOCAL	AQAAAAIAAYagAAAAEMa9qd78Fhm+i6WlQIWbA0oTRemrrwoIP/XwOmaoEFH4y54oHQg5favZTIwG7ixFcQ==	STAMP_MKT_MGR	Vũ Phương Thảo (Trưởng phòng Marketing)	019f8000-0000-7000-8000-000000000006	ACTIVE	0	\N	f	\N	2026-11-01 17:13:19.890999+00	2026-11-01 17:13:19.890999+00	\N
019f8000-0000-7000-8000-000000000017	019f8000-0000-7000-8000-000000000002	f	MKT001	MKT001	linh.le@bizflow.local	LINH.LE@BIZFLOW.LOCAL	AQAAAAIAAYagAAAAEMa9qd78Fhm+i6WlQIWbA0oTRemrrwoIP/XwOmaoEFH4y54oHQg5favZTIwG7ixFcQ==	STAMP_MKT001	Lê Mỹ Linh (Chuyên viên Content)	019f8000-0000-7000-8000-000000000006	ACTIVE	0	\N	f	\N	2026-11-01 17:13:19.892534+00	2026-11-01 17:13:19.892534+00	\N
019f8000-0000-7000-8000-000000000018	019f8000-0000-7000-8000-000000000002	f	FIN_MGR	FIN_MGR	ha.dang@bizflow.local	HA.DANG@BIZFLOW.LOCAL	AQAAAAIAAYagAAAAEMa9qd78Fhm+i6WlQIWbA0oTRemrrwoIP/XwOmaoEFH4y54oHQg5favZTIwG7ixFcQ==	STAMP_FIN_MGR	Đặng Thu Hà (Kế toán trưởng)	019f8000-0000-7000-8000-000000000007	ACTIVE	0	\N	f	\N	2026-11-01 17:13:19.894798+00	2026-11-01 17:13:19.894798+00	\N
019f8000-0000-7000-8000-000000000019	019f8000-0000-7000-8000-000000000002	f	ACC001	ACC001	anh.phan@bizflow.local	ANH.PHAN@BIZFLOW.LOCAL	AQAAAAIAAYagAAAAEMa9qd78Fhm+i6WlQIWbA0oTRemrrwoIP/XwOmaoEFH4y54oHQg5favZTIwG7ixFcQ==	STAMP_ACC001	Phan Ngọc Ánh (Kế toán viên)	019f8000-0000-7000-8000-000000000007	ACTIVE	0	\N	f	\N	2026-11-01 17:13:19.895943+00	2026-11-01 17:13:19.895943+00	\N
019f8000-0000-7000-8000-00000000001a	019f8000-0000-7000-8000-000000000002	f	HR_MGR	HR_MGR	lan.bui@bizflow.local	LAN.BUI@BIZFLOW.LOCAL	AQAAAAIAAYagAAAAEMa9qd78Fhm+i6WlQIWbA0oTRemrrwoIP/XwOmaoEFH4y54oHQg5favZTIwG7ixFcQ==	STAMP_HR_MGR	Bùi Mai Lan (Trưởng phòng Nhân sự)	019f8000-0000-7000-8000-000000000008	ACTIVE	0	\N	f	\N	2026-11-01 17:13:19.896977+00	2026-11-01 17:13:19.896977+00	\N
019f8000-0000-7000-8000-00000000001b	019f8000-0000-7000-8000-000000000002	f	HR001	HR001	thanh.ngo@bizflow.local	THANH.NGO@BIZFLOW.LOCAL	AQAAAAIAAYagAAAAEMa9qd78Fhm+i6WlQIWbA0oTRemrrwoIP/XwOmaoEFH4y54oHQg5favZTIwG7ixFcQ==	STAMP_HR001	Ngô Thị Thanh (Chuyên viên Tuyển dụng)	019f8000-0000-7000-8000-000000000008	ACTIVE	0	\N	f	\N	2026-11-01 17:13:19.897828+00	2026-11-01 17:13:19.897828+00	\N
019f8000-0000-7000-8000-00000000001c	019f8000-0000-7000-8000-000000000002	f	CS_MGR	CS_MGR	bao.trinh@bizflow.local	BAO.TRINH@BIZFLOW.LOCAL	AQAAAAIAAYagAAAAEMa9qd78Fhm+i6WlQIWbA0oTRemrrwoIP/XwOmaoEFH4y54oHQg5favZTIwG7ixFcQ==	STAMP_CS_MGR	Trịnh Quốc Bảo (Trưởng phòng CSKH)	019f8000-0000-7000-8000-000000000009	ACTIVE	0	\N	f	\N	2026-11-01 17:13:19.898793+00	2026-11-01 17:13:19.898793+00	\N
019f8000-0000-7000-8000-00000000001d	019f8000-0000-7000-8000-000000000002	f	CS001	CS001	binh.tran@bizflow.local	BINH.TRAN@BIZFLOW.LOCAL	AQAAAAIAAYagAAAAEMa9qd78Fhm+i6WlQIWbA0oTRemrrwoIP/XwOmaoEFH4y54oHQg5favZTIwG7ixFcQ==	STAMP_CS001	Trần Văn Bình (Chuyên viên CSKH VIP)	019f8000-0000-7000-8000-000000000009	ACTIVE	0	\N	f	\N	2026-11-01 17:13:19.899848+00	2026-11-01 17:13:19.899848+00	\N
019f8000-0000-7000-8000-000000000012	019f8000-0000-7000-8000-000000000002	f	MGR001	MGR001	manager@bizflow.local	MANAGER@BIZFLOW.LOCAL	AQAAAAIAAYagAAAAEMa9qd78Fhm+i6WlQIWbA0oTRemrrwoIP/XwOmaoEFH4y54oHQg5favZTIwG7ixFcQ==	019f8000000070008000000000000012	Lê Hoàng Nam (CTO)	019f8000-0000-7000-8000-000000000003	ACTIVE	0	\N	f	2026-11-01 17:28:04.243477+00	2026-11-01 17:13:15.981302+00	2026-11-01 17:28:04.243477+00	\N
019f8000-0000-7000-8000-000000000011	019f8000-0000-7000-8000-000000000002	f	EMP001	EMP001	employee@bizflow.local	EMPLOYEE@BIZFLOW.LOCAL	AQAAAAIAAYagAAAAEMa9qd78Fhm+i6WlQIWbA0oTRemrrwoIP/XwOmaoEFH4y54oHQg5favZTIwG7ixFcQ==	019f8000000070008000000000000011	Nguyễn Văn Nhân Viên (Dev)	019f8000-0000-7000-8000-000000000003	ACTIVE	0	\N	f	2026-11-01 17:28:13.597812+00	2026-11-01 17:13:15.980042+00	2026-11-01 17:28:13.597812+00	\N
\.


--
-- Data for Name: UserRole; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public."UserRole" ("UserId", "RoleId") FROM stdin;
019f8000-0000-7000-8000-000000000010	019f7f8a-0000-7000-8000-000000000002
019f8000-0000-7000-8000-000000000010	019f7f8a-0000-7000-8000-000000000003
019f8000-0000-7000-8000-000000000010	019f7f8a-0000-7000-8000-000000000004
019f8000-0000-7000-8000-000000000012	019f7f8a-0000-7000-8000-000000000003
019f8000-0000-7000-8000-000000000012	019f7f8a-0000-7000-8000-000000000004
019f8000-0000-7000-8000-000000000011	019f7f8a-0000-7000-8000-000000000004
019f8000-0000-7000-8000-000000000013	019f7f8a-0000-7000-8000-000000000002
019f8000-0000-7000-8000-000000000013	019f7f8a-0000-7000-8000-000000000003
019f8000-0000-7000-8000-000000000013	019f7f8a-0000-7000-8000-000000000004
019f8000-0000-7000-8000-000000000014	019f7f8a-0000-7000-8000-000000000003
019f8000-0000-7000-8000-000000000014	019f7f8a-0000-7000-8000-000000000004
019f8000-0000-7000-8000-000000000016	019f7f8a-0000-7000-8000-000000000003
019f8000-0000-7000-8000-000000000016	019f7f8a-0000-7000-8000-000000000004
019f8000-0000-7000-8000-000000000018	019f7f8a-0000-7000-8000-000000000003
019f8000-0000-7000-8000-000000000018	019f7f8a-0000-7000-8000-000000000004
019f8000-0000-7000-8000-00000000001a	019f7f8a-0000-7000-8000-000000000003
019f8000-0000-7000-8000-00000000001a	019f7f8a-0000-7000-8000-000000000004
019f8000-0000-7000-8000-00000000001c	019f7f8a-0000-7000-8000-000000000003
019f8000-0000-7000-8000-00000000001c	019f7f8a-0000-7000-8000-000000000004
019f8000-0000-7000-8000-00000000001e	019f7f8a-0000-7000-8000-000000000004
019f8000-0000-7000-8000-000000000015	019f7f8a-0000-7000-8000-000000000004
019f8000-0000-7000-8000-000000000017	019f7f8a-0000-7000-8000-000000000004
019f8000-0000-7000-8000-000000000019	019f7f8a-0000-7000-8000-000000000004
019f8000-0000-7000-8000-00000000001b	019f7f8a-0000-7000-8000-000000000004
019f8000-0000-7000-8000-00000000001d	019f7f8a-0000-7000-8000-000000000004
\.


--
-- Data for Name: WebPushSubscription; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public."WebPushSubscription" ("Id", "TenantId", "UserId", "Endpoint", "P256dh", "Auth", "CreatedAt", "LastActiveAt") FROM stdin;
\.


--
-- Data for Name: Workflow; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public."Workflow" ("WorkflowId", "TenantId", "Name", "BusinessType", "Status", "CreatedAt", "UpdatedAt") FROM stdin;
\.


--
-- Data for Name: WorkflowStep; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public."WorkflowStep" ("WorkflowStepId", "WorkflowVersionId", "StepCode", "Name", "Type", "OrderNo", "ConfigJson") FROM stdin;
\.


--
-- Data for Name: WorkflowTransition; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public."WorkflowTransition" ("TransitionId", "WorkflowVersionId", "FromState", "ToState", "GuardJson") FROM stdin;
\.


--
-- Data for Name: WorkflowVersion; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public."WorkflowVersion" ("WorkflowVersionId", "WorkflowId", "VersionNo", "Status", "PublishedAt", "DefinitionJson") FROM stdin;
\.


--
-- Data for Name: __EFMigrationsHistory; Type: TABLE DATA; Schema: public; Owner: -
--

COPY public."__EFMigrationsHistory" ("MigrationId", "ProductVersion") FROM stdin;
20260929154550_IdentityFoundation	10.0.12
20260930055252_PlatformRegistrationRead	10.0.12
20260930175555_TenantRoleConfiguration	10.0.12
20261001012648_ManagementScopeFoundation	10.0.12
20261001013618_UserDirectoryRead	10.0.12
20261001090500_WorkflowPersistenceFoundation	10.0.12
20261001092159_WorkflowLibraryPermissions	10.0.12
20261001094232_AuditReadPermissions	10.0.12
20261001193054_NotificationInbox	10.0.12
20261002123538_TenantSettingFoundation	10.0.12
20261002125924_SlaProfileCatalog	10.0.12
20261002182914_SlaSnapshotFoundation	10.0.12
20261003022840_ServiceCatalogFoundation	10.0.12
20261003165823_RequestDraftFoundation	10.0.12
20261003172253_ServiceMetadataUpdatePermission	10.0.12
20261004075754_RequestResolutionHistory	10.0.12
20261004081325_TaskDraftFoundation	10.0.12
20261004133416_TaskEvidenceHistory	10.0.12
20261004134916_TaskAssignmentHistory	10.0.12
20261005003438_ScopedTaskReadPermissions	10.0.12
20261005055653_TaskCreationPermission	10.0.12
20261006033038_TaskCreationReplayProtection	10.0.12
20261006092254_TaskAssignmentCommand	10.0.12
20261006143521_TaskAcceptanceCommand	10.0.12
20261006191621_TaskExecutionCommand	10.0.12
20261007120000_TaskResultAndConfirmationCommand	10.0.12
20261007140000_RequestCreationAndSubmissionCommand	10.0.12
20261007160000_RequestLifecycleAndRoutingCommand	10.0.12
20261007160000_RequestResolutionAndConfirmationCommand	10.0.12
20261007200000_CommentFoundation	10.0.12
20261007220000_AttachmentFoundation	10.0.12
20261007230000_RecordArchivalAndSearchFoundation	10.0.12
20261008010000_DashboardAndReportingFoundation	10.0.12
20261008030000_AiAssistedOperationsFoundation	10.0.12
20261021100000_CompanyRegistration6FieldsAndEvidence	10.0.12
20261021110000_DeadlineRemindersAndWebPush	10.0.12
20261030233450_AiVectorMemoryFoundation	10.0.12
\.


--
-- Name: Department AK_Department_TenantId_DepartmentId; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Department"
    ADD CONSTRAINT "AK_Department_TenantId_DepartmentId" UNIQUE ("TenantId", "DepartmentId");


--
-- Name: AIAgentAction PK_AIAgentAction; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."AIAgentAction"
    ADD CONSTRAINT "PK_AIAgentAction" PRIMARY KEY ("AIAgentActionId");


--
-- Name: AIInteraction PK_AIInteraction; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."AIInteraction"
    ADD CONSTRAINT "PK_AIInteraction" PRIMARY KEY ("AIInteractionId");


--
-- Name: AIRecommendation PK_AIRecommendation; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."AIRecommendation"
    ADD CONSTRAINT "PK_AIRecommendation" PRIMARY KEY ("RecommendationId");


--
-- Name: AIVectorMemory PK_AIVectorMemory; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."AIVectorMemory"
    ADD CONSTRAINT "PK_AIVectorMemory" PRIMARY KEY ("MemoryId");


--
-- Name: Attachment PK_Attachment; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Attachment"
    ADD CONSTRAINT "PK_Attachment" PRIMARY KEY ("AttachmentId");


--
-- Name: AuditLog PK_AuditLog; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."AuditLog"
    ADD CONSTRAINT "PK_AuditLog" PRIMARY KEY ("AuditLogId");


--
-- Name: AuthenticationSession PK_AuthenticationSession; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."AuthenticationSession"
    ADD CONSTRAINT "PK_AuthenticationSession" PRIMARY KEY ("AuthenticationSessionId");


--
-- Name: BusinessCalendar PK_BusinessCalendar; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."BusinessCalendar"
    ADD CONSTRAINT "PK_BusinessCalendar" PRIMARY KEY ("CalendarId");


--
-- Name: Comment PK_Comment; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Comment"
    ADD CONSTRAINT "PK_Comment" PRIMARY KEY ("CommentId");


--
-- Name: Company PK_Company; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Company"
    ADD CONSTRAINT "PK_Company" PRIMARY KEY ("CompanyId");


--
-- Name: Confirmation PK_Confirmation; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Confirmation"
    ADD CONSTRAINT "PK_Confirmation" PRIMARY KEY ("ConfirmationId");


--
-- Name: DeadlineReminderSchedule PK_DeadlineReminderSchedule; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."DeadlineReminderSchedule"
    ADD CONSTRAINT "PK_DeadlineReminderSchedule" PRIMARY KEY ("Id");


--
-- Name: Department PK_Department; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Department"
    ADD CONSTRAINT "PK_Department" PRIMARY KEY ("DepartmentId");


--
-- Name: ManagementScope PK_ManagementScope; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ManagementScope"
    ADD CONSTRAINT "PK_ManagementScope" PRIMARY KEY ("ManagementScopeId");


--
-- Name: Notification PK_Notification; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Notification"
    ADD CONSTRAINT "PK_Notification" PRIMARY KEY ("NotificationId");


--
-- Name: NotificationLog PK_NotificationLog; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."NotificationLog"
    ADD CONSTRAINT "PK_NotificationLog" PRIMARY KEY ("Id");


--
-- Name: Permission PK_Permission; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Permission"
    ADD CONSTRAINT "PK_Permission" PRIMARY KEY ("PermissionId");


--
-- Name: Request PK_Request; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Request"
    ADD CONSTRAINT "PK_Request" PRIMARY KEY ("RequestId");


--
-- Name: RequestResolution PK_RequestResolution; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."RequestResolution"
    ADD CONSTRAINT "PK_RequestResolution" PRIMARY KEY ("ResolutionId");


--
-- Name: RequestRouting PK_RequestRouting; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."RequestRouting"
    ADD CONSTRAINT "PK_RequestRouting" PRIMARY KEY ("RequestRoutingId");


--
-- Name: Role PK_Role; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Role"
    ADD CONSTRAINT "PK_Role" PRIMARY KEY ("RoleId");


--
-- Name: RolePermission PK_RolePermission; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."RolePermission"
    ADD CONSTRAINT "PK_RolePermission" PRIMARY KEY ("RoleId", "PermissionId");


--
-- Name: SLAProfile PK_SLAProfile; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."SLAProfile"
    ADD CONSTRAINT "PK_SLAProfile" PRIMARY KEY ("SLAProfileId");


--
-- Name: SLAVersion PK_SLAVersion; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."SLAVersion"
    ADD CONSTRAINT "PK_SLAVersion" PRIMARY KEY ("SLAVersionId");


--
-- Name: Service PK_Service; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Service"
    ADD CONSTRAINT "PK_Service" PRIMARY KEY ("ServiceId");


--
-- Name: ServiceCategory PK_ServiceCategory; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ServiceCategory"
    ADD CONSTRAINT "PK_ServiceCategory" PRIMARY KEY ("ServiceCategoryId");


--
-- Name: Task PK_Task; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Task"
    ADD CONSTRAINT "PK_Task" PRIMARY KEY ("TaskId");


--
-- Name: TaskAssignment PK_TaskAssignment; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."TaskAssignment"
    ADD CONSTRAINT "PK_TaskAssignment" PRIMARY KEY ("TaskAssignmentId");


--
-- Name: TaskChecklistItem PK_TaskChecklistItem; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."TaskChecklistItem"
    ADD CONSTRAINT "PK_TaskChecklistItem" PRIMARY KEY ("ChecklistItemId");


--
-- Name: TaskProgressReport PK_TaskProgressReport; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."TaskProgressReport"
    ADD CONSTRAINT "PK_TaskProgressReport" PRIMARY KEY ("ProgressReportId");


--
-- Name: TaskResult PK_TaskResult; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."TaskResult"
    ADD CONSTRAINT "PK_TaskResult" PRIMARY KEY ("TaskResultId");


--
-- Name: Tenant PK_Tenant; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Tenant"
    ADD CONSTRAINT "PK_Tenant" PRIMARY KEY ("TenantId");


--
-- Name: TenantSetting PK_TenantSetting; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."TenantSetting"
    ADD CONSTRAINT "PK_TenantSetting" PRIMARY KEY ("TenantSettingId");


--
-- Name: User PK_User; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."User"
    ADD CONSTRAINT "PK_User" PRIMARY KEY ("UserId");


--
-- Name: UserRole PK_UserRole; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."UserRole"
    ADD CONSTRAINT "PK_UserRole" PRIMARY KEY ("UserId", "RoleId");


--
-- Name: WebPushSubscription PK_WebPushSubscription; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."WebPushSubscription"
    ADD CONSTRAINT "PK_WebPushSubscription" PRIMARY KEY ("Id");


--
-- Name: Workflow PK_Workflow; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Workflow"
    ADD CONSTRAINT "PK_Workflow" PRIMARY KEY ("WorkflowId");


--
-- Name: WorkflowStep PK_WorkflowStep; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."WorkflowStep"
    ADD CONSTRAINT "PK_WorkflowStep" PRIMARY KEY ("WorkflowStepId");


--
-- Name: WorkflowTransition PK_WorkflowTransition; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."WorkflowTransition"
    ADD CONSTRAINT "PK_WorkflowTransition" PRIMARY KEY ("TransitionId");


--
-- Name: WorkflowVersion PK_WorkflowVersion; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."WorkflowVersion"
    ADD CONSTRAINT "PK_WorkflowVersion" PRIMARY KEY ("WorkflowVersionId");


--
-- Name: __EFMigrationsHistory PK___EFMigrationsHistory; Type: CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."__EFMigrationsHistory"
    ADD CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId");


--
-- Name: IX_AIAgentAction_TenantId_AIInteractionId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_AIAgentAction_TenantId_AIInteractionId" ON public."AIAgentAction" USING btree ("TenantId", "AIInteractionId");


--
-- Name: IX_AIAgentAction_TenantId_CreatedAt; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_AIAgentAction_TenantId_CreatedAt" ON public."AIAgentAction" USING btree ("TenantId", "CreatedAt");


--
-- Name: IX_AIInteraction_TenantId_CreatedAt; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_AIInteraction_TenantId_CreatedAt" ON public."AIInteraction" USING btree ("TenantId", "CreatedAt");


--
-- Name: IX_AIInteraction_TenantId_UserId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_AIInteraction_TenantId_UserId" ON public."AIInteraction" USING btree ("TenantId", "UserId");


--
-- Name: IX_AIRecommendation_TenantId_AIInteractionId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_AIRecommendation_TenantId_AIInteractionId" ON public."AIRecommendation" USING btree ("TenantId", "AIInteractionId");


--
-- Name: IX_AIRecommendation_TenantId_ObjectType_ObjectId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_AIRecommendation_TenantId_ObjectType_ObjectId" ON public."AIRecommendation" USING btree ("TenantId", "ObjectType", "ObjectId");


--
-- Name: IX_AIVectorMemory_TenantId_CreatedAt; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_AIVectorMemory_TenantId_CreatedAt" ON public."AIVectorMemory" USING btree ("TenantId", "CreatedAt");


--
-- Name: IX_AIVectorMemory_TenantId_MemoryScope; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_AIVectorMemory_TenantId_MemoryScope" ON public."AIVectorMemory" USING btree ("TenantId", "MemoryScope");


--
-- Name: IX_Attachment_TenantId_ObjectKey; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_Attachment_TenantId_ObjectKey" ON public."Attachment" USING btree ("TenantId", "ObjectKey");


--
-- Name: IX_Attachment_TenantId_ObjectType_ObjectId_CreatedAt; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Attachment_TenantId_ObjectType_ObjectId_CreatedAt" ON public."Attachment" USING btree ("TenantId", "ObjectType", "ObjectId", "CreatedAt");


--
-- Name: IX_Attachment_UploadedBy; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Attachment_UploadedBy" ON public."Attachment" USING btree ("UploadedBy");


--
-- Name: IX_AuditLog_TenantId_CreatedAt_AuditLogId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_AuditLog_TenantId_CreatedAt_AuditLogId" ON public."AuditLog" USING btree ("TenantId", "CreatedAt", "AuditLogId");


--
-- Name: IX_AuthenticationSession_ExpiresAt; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_AuthenticationSession_ExpiresAt" ON public."AuthenticationSession" USING btree ("ExpiresAt");


--
-- Name: IX_AuthenticationSession_FamilyId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_AuthenticationSession_FamilyId" ON public."AuthenticationSession" USING btree ("FamilyId");


--
-- Name: IX_AuthenticationSession_ReplacedById; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_AuthenticationSession_ReplacedById" ON public."AuthenticationSession" USING btree ("ReplacedById");


--
-- Name: IX_AuthenticationSession_TokenHash; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_AuthenticationSession_TokenHash" ON public."AuthenticationSession" USING btree ("TokenHash");


--
-- Name: IX_AuthenticationSession_UserId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_AuthenticationSession_UserId" ON public."AuthenticationSession" USING btree ("UserId");


--
-- Name: IX_BusinessCalendar_TenantId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_BusinessCalendar_TenantId" ON public."BusinessCalendar" USING btree ("TenantId");


--
-- Name: IX_Comment_AuthorId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Comment_AuthorId" ON public."Comment" USING btree ("AuthorId");


--
-- Name: IX_Comment_TenantId_ObjectType_ObjectId_CreatedAt; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Comment_TenantId_ObjectType_ObjectId_CreatedAt" ON public."Comment" USING btree ("TenantId", "ObjectType", "ObjectId", "CreatedAt");


--
-- Name: IX_Company_Code; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_Company_Code" ON public."Company" USING btree ("Code");


--
-- Name: IX_Confirmation_ActorId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Confirmation_ActorId" ON public."Confirmation" USING btree ("ActorId");


--
-- Name: IX_Confirmation_TenantId_ObjectType_ObjectId_ConfirmedAt; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Confirmation_TenantId_ObjectType_ObjectId_ConfirmedAt" ON public."Confirmation" USING btree ("TenantId", "ObjectType", "ObjectId", "ConfirmedAt");


--
-- Name: IX_DeadlineReminderSchedule_TenantId_Status_RemindAt; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_DeadlineReminderSchedule_TenantId_Status_RemindAt" ON public."DeadlineReminderSchedule" USING btree ("TenantId", "Status", "RemindAt");


--
-- Name: IX_DeadlineReminderSchedule_TenantId_TaskId_UserId_TriggerType; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_DeadlineReminderSchedule_TenantId_TaskId_UserId_TriggerType" ON public."DeadlineReminderSchedule" USING btree ("TenantId", "TaskId", "UserId", "TriggerType");


--
-- Name: IX_Department_TenantId_Code; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_Department_TenantId_Code" ON public."Department" USING btree ("TenantId", "Code");


--
-- Name: IX_Department_TenantId_ParentDepartmentId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Department_TenantId_ParentDepartmentId" ON public."Department" USING btree ("TenantId", "ParentDepartmentId");


--
-- Name: IX_ManagementScope_CreatedBy; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_ManagementScope_CreatedBy" ON public."ManagementScope" USING btree ("CreatedBy");


--
-- Name: IX_ManagementScope_TenantId_DepartmentId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_ManagementScope_TenantId_DepartmentId" ON public."ManagementScope" USING btree ("TenantId", "DepartmentId");


--
-- Name: IX_ManagementScope_TenantId_UserId_DepartmentId; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_ManagementScope_TenantId_UserId_DepartmentId" ON public."ManagementScope" USING btree ("TenantId", "UserId", "DepartmentId");


--
-- Name: IX_ManagementScope_UserId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_ManagementScope_UserId" ON public."ManagementScope" USING btree ("UserId");


--
-- Name: IX_NotificationLog_TenantId_TaskId_UserId_TriggerType_Scheduled; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_NotificationLog_TenantId_TaskId_UserId_TriggerType_Scheduled" ON public."NotificationLog" USING btree ("TenantId", "TaskId", "UserId", "TriggerType", "ScheduledAt");


--
-- Name: IX_Notification_RecipientId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Notification_RecipientId" ON public."Notification" USING btree ("RecipientId");


--
-- Name: IX_Notification_TenantId_IdempotencyKey; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_Notification_TenantId_IdempotencyKey" ON public."Notification" USING btree ("TenantId", "IdempotencyKey");


--
-- Name: IX_Notification_TenantId_RecipientId_NotificationId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Notification_TenantId_RecipientId_NotificationId" ON public."Notification" USING btree ("TenantId", "RecipientId", "NotificationId");


--
-- Name: IX_Notification_TenantId_RecipientId_ReadAt; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Notification_TenantId_RecipientId_ReadAt" ON public."Notification" USING btree ("TenantId", "RecipientId", "ReadAt");


--
-- Name: IX_Permission_Code; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_Permission_Code" ON public."Permission" USING btree ("Code");


--
-- Name: IX_RequestResolution_RequestId_RevisionNo; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_RequestResolution_RequestId_RevisionNo" ON public."RequestResolution" USING btree ("RequestId", "RevisionNo");


--
-- Name: IX_RequestResolution_ResolverId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_RequestResolution_ResolverId" ON public."RequestResolution" USING btree ("ResolverId");


--
-- Name: IX_RequestRouting_TenantId_RequestId_RoutedAt; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_RequestRouting_TenantId_RequestId_RoutedAt" ON public."RequestRouting" USING btree ("TenantId", "RequestId", "RoutedAt");


--
-- Name: IX_Request_CategoryId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Request_CategoryId" ON public."Request" USING btree ("CategoryId");


--
-- Name: IX_Request_ParentRequestId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Request_ParentRequestId" ON public."Request" USING btree ("ParentRequestId");


--
-- Name: IX_Request_RequesterId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Request_RequesterId" ON public."Request" USING btree ("RequesterId");


--
-- Name: IX_Request_RevisedFromRequestId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Request_RevisedFromRequestId" ON public."Request" USING btree ("RevisedFromRequestId");


--
-- Name: IX_Request_SLAVersionId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Request_SLAVersionId" ON public."Request" USING btree ("SLAVersionId");


--
-- Name: IX_Request_ServiceId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Request_ServiceId" ON public."Request" USING btree ("ServiceId");


--
-- Name: IX_Request_TenantId_RequesterId_CreatedAt; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Request_TenantId_RequesterId_CreatedAt" ON public."Request" USING btree ("TenantId", "RequesterId", "CreatedAt");


--
-- Name: IX_Request_TenantId_Status_CreatedAt; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Request_TenantId_Status_CreatedAt" ON public."Request" USING btree ("TenantId", "Status", "CreatedAt");


--
-- Name: IX_Request_Tenant_DeletedAt; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Request_Tenant_DeletedAt" ON public."Request" USING btree ("TenantId", "DeletedAt");


--
-- Name: IX_Request_WorkflowVersionId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Request_WorkflowVersionId" ON public."Request" USING btree ("WorkflowVersionId");


--
-- Name: IX_RolePermission_PermissionId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_RolePermission_PermissionId" ON public."RolePermission" USING btree ("PermissionId");


--
-- Name: IX_Role_TenantId_Name; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_Role_TenantId_Name" ON public."Role" USING btree ("TenantId", "Name") NULLS NOT DISTINCT;


--
-- Name: IX_SLAProfile_TenantId_Status_Name; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_SLAProfile_TenantId_Status_Name" ON public."SLAProfile" USING btree ("TenantId", "Status", "Name");


--
-- Name: IX_SLAVersion_CalendarId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_SLAVersion_CalendarId" ON public."SLAVersion" USING btree ("CalendarId");


--
-- Name: IX_SLAVersion_SLAProfileId_VersionNo; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_SLAVersion_SLAProfileId_VersionNo" ON public."SLAVersion" USING btree ("SLAProfileId", "VersionNo");


--
-- Name: IX_ServiceCategory_ServiceId_Code; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_ServiceCategory_ServiceId_Code" ON public."ServiceCategory" USING btree ("ServiceId", "Code");


--
-- Name: IX_Service_ActiveSLAVersionId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Service_ActiveSLAVersionId" ON public."Service" USING btree ("ActiveSLAVersionId");


--
-- Name: IX_Service_ActiveWorkflowVersionId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Service_ActiveWorkflowVersionId" ON public."Service" USING btree ("ActiveWorkflowVersionId");


--
-- Name: IX_Service_TenantId_Code; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_Service_TenantId_Code" ON public."Service" USING btree ("TenantId", "Code");


--
-- Name: IX_Service_TenantId_Status_Name; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Service_TenantId_Status_Name" ON public."Service" USING btree ("TenantId", "Status", "Name");


--
-- Name: IX_TaskAssignment_AssignedBy; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_TaskAssignment_AssignedBy" ON public."TaskAssignment" USING btree ("AssignedBy");


--
-- Name: IX_TaskAssignment_DepartmentId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_TaskAssignment_DepartmentId" ON public."TaskAssignment" USING btree ("DepartmentId");


--
-- Name: IX_TaskAssignment_TaskId; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_TaskAssignment_TaskId" ON public."TaskAssignment" USING btree ("TaskId") WHERE ("EndedAt" IS NULL);


--
-- Name: IX_TaskAssignment_TaskId_AssignedAt; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_TaskAssignment_TaskId_AssignedAt" ON public."TaskAssignment" USING btree ("TaskId", "AssignedAt");


--
-- Name: IX_TaskAssignment_UserId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_TaskAssignment_UserId" ON public."TaskAssignment" USING btree ("UserId");


--
-- Name: IX_TaskChecklistItem_CompletedBy; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_TaskChecklistItem_CompletedBy" ON public."TaskChecklistItem" USING btree ("CompletedBy");


--
-- Name: IX_TaskChecklistItem_TaskId_SortOrder; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_TaskChecklistItem_TaskId_SortOrder" ON public."TaskChecklistItem" USING btree ("TaskId", "SortOrder");


--
-- Name: IX_TaskProgressReport_AuthorId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_TaskProgressReport_AuthorId" ON public."TaskProgressReport" USING btree ("AuthorId");


--
-- Name: IX_TaskProgressReport_TaskId_SubmittedAt; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_TaskProgressReport_TaskId_SubmittedAt" ON public."TaskProgressReport" USING btree ("TaskId", "SubmittedAt");


--
-- Name: IX_TaskResult_AuthorId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_TaskResult_AuthorId" ON public."TaskResult" USING btree ("AuthorId");


--
-- Name: IX_TaskResult_TaskId_RevisionNo; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_TaskResult_TaskId_RevisionNo" ON public."TaskResult" USING btree ("TaskId", "RevisionNo");


--
-- Name: IX_Task_CreatorId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Task_CreatorId" ON public."Task" USING btree ("CreatorId");


--
-- Name: IX_Task_RequestId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Task_RequestId" ON public."Task" USING btree ("RequestId");


--
-- Name: IX_Task_SLAVersionId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Task_SLAVersionId" ON public."Task" USING btree ("SLAVersionId");


--
-- Name: IX_Task_TenantId_CreatorId_CreatedAt; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Task_TenantId_CreatorId_CreatedAt" ON public."Task" USING btree ("TenantId", "CreatorId", "CreatedAt");


--
-- Name: IX_Task_TenantId_Status_Deadline; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Task_TenantId_Status_Deadline" ON public."Task" USING btree ("TenantId", "Status", "Deadline");


--
-- Name: IX_Task_Tenant_DeletedAt; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Task_Tenant_DeletedAt" ON public."Task" USING btree ("TenantId", "DeletedAt");


--
-- Name: IX_Task_WorkflowVersionId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Task_WorkflowVersionId" ON public."Task" USING btree ("WorkflowVersionId");


--
-- Name: IX_TenantSetting_TenantId_Key; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_TenantSetting_TenantId_Key" ON public."TenantSetting" USING btree ("TenantId", "Key");


--
-- Name: IX_TenantSetting_UpdatedBy; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_TenantSetting_UpdatedBy" ON public."TenantSetting" USING btree ("UpdatedBy");


--
-- Name: IX_Tenant_CompanyId; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_Tenant_CompanyId" ON public."Tenant" USING btree ("CompanyId");


--
-- Name: IX_Tenant_TenantKey; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_Tenant_TenantKey" ON public."Tenant" USING btree ("TenantKey");


--
-- Name: IX_UserRole_RoleId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_UserRole_RoleId" ON public."UserRole" USING btree ("RoleId");


--
-- Name: IX_User_TenantId_DepartmentId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_User_TenantId_DepartmentId" ON public."User" USING btree ("TenantId", "DepartmentId");


--
-- Name: IX_User_TenantId_NormalizedEmail; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_User_TenantId_NormalizedEmail" ON public."User" USING btree ("TenantId", "NormalizedEmail") NULLS NOT DISTINCT;


--
-- Name: IX_User_TenantId_NormalizedEmployeeCode; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_User_TenantId_NormalizedEmployeeCode" ON public."User" USING btree ("TenantId", "NormalizedEmployeeCode") NULLS NOT DISTINCT;


--
-- Name: IX_WebPushSubscription_TenantId_UserId_Endpoint; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_WebPushSubscription_TenantId_UserId_Endpoint" ON public."WebPushSubscription" USING btree ("TenantId", "UserId", "Endpoint");


--
-- Name: IX_WorkflowStep_WorkflowVersionId_OrderNo; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_WorkflowStep_WorkflowVersionId_OrderNo" ON public."WorkflowStep" USING btree ("WorkflowVersionId", "OrderNo");


--
-- Name: IX_WorkflowStep_WorkflowVersionId_StepCode; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_WorkflowStep_WorkflowVersionId_StepCode" ON public."WorkflowStep" USING btree ("WorkflowVersionId", "StepCode");


--
-- Name: IX_WorkflowTransition_WorkflowVersionId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_WorkflowTransition_WorkflowVersionId" ON public."WorkflowTransition" USING btree ("WorkflowVersionId");


--
-- Name: IX_WorkflowVersion_WorkflowId_VersionNo; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "IX_WorkflowVersion_WorkflowId_VersionNo" ON public."WorkflowVersion" USING btree ("WorkflowId", "VersionNo");


--
-- Name: IX_Workflow_TenantId; Type: INDEX; Schema: public; Owner: -
--

CREATE INDEX "IX_Workflow_TenantId" ON public."Workflow" USING btree ("TenantId");


--
-- Name: UX_AuditLog_AttachmentUploadSessionReplay; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "UX_AuditLog_AttachmentUploadSessionReplay" ON public."AuditLog" USING btree ("TenantId", "ActorId", ((("MetadataJson" -> 'idempotency'::text) ->> 'keyHash'::text))) WHERE ((("Action")::text = 'ATTACHMENT.UPLOAD_SESSION_CREATED'::text) AND ((("MetadataJson" -> 'idempotency'::text) ->> 'keyHash'::text) IS NOT NULL));


--
-- Name: UX_AuditLog_CommentCreationReplay; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "UX_AuditLog_CommentCreationReplay" ON public."AuditLog" USING btree ("TenantId", "ActorId", ((("MetadataJson" -> 'idempotency'::text) ->> 'keyHash'::text))) WHERE ((("Action")::text = 'COMMENT.CREATED'::text) AND ((("MetadataJson" -> 'idempotency'::text) ->> 'keyHash'::text) IS NOT NULL));


--
-- Name: UX_AuditLog_RequestConfirmationReplay; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "UX_AuditLog_RequestConfirmationReplay" ON public."AuditLog" USING btree ("TenantId", "ActorId", ((("MetadataJson" -> 'idempotency'::text) ->> 'keyHash'::text))) WHERE ((("Action")::text = 'REQUEST.CONFIRMED'::text) AND ((("MetadataJson" -> 'idempotency'::text) ->> 'keyHash'::text) IS NOT NULL));


--
-- Name: UX_AuditLog_RequestCreationReplay; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "UX_AuditLog_RequestCreationReplay" ON public."AuditLog" USING btree ("TenantId", "ActorId", ((("MetadataJson" -> 'idempotency'::text) ->> 'keyHash'::text))) WHERE ((("Action")::text = 'REQUEST.CREATED'::text) AND ((("MetadataJson" -> 'idempotency'::text) ->> 'keyHash'::text) IS NOT NULL));


--
-- Name: UX_AuditLog_RequestExecutionReplay; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "UX_AuditLog_RequestExecutionReplay" ON public."AuditLog" USING btree ("TenantId", "ActorId", ((("MetadataJson" -> 'idempotency'::text) ->> 'keyHash'::text))) WHERE ((("Action")::text = 'REQUEST.STARTED'::text) AND ((("MetadataJson" -> 'idempotency'::text) ->> 'keyHash'::text) IS NOT NULL));


--
-- Name: UX_AuditLog_RequestReceiptReplay; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "UX_AuditLog_RequestReceiptReplay" ON public."AuditLog" USING btree ("TenantId", "ActorId", ((("MetadataJson" -> 'idempotency'::text) ->> 'keyHash'::text))) WHERE ((("Action")::text = 'REQUEST.RECEIVED'::text) AND ((("MetadataJson" -> 'idempotency'::text) ->> 'keyHash'::text) IS NOT NULL));


--
-- Name: UX_AuditLog_RequestResolutionReplay; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "UX_AuditLog_RequestResolutionReplay" ON public."AuditLog" USING btree ("TenantId", "ActorId", ((("MetadataJson" -> 'idempotency'::text) ->> 'keyHash'::text))) WHERE ((("Action")::text = 'REQUEST.RESOLVED'::text) AND ((("MetadataJson" -> 'idempotency'::text) ->> 'keyHash'::text) IS NOT NULL));


--
-- Name: UX_AuditLog_RequestRevisionReplay; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "UX_AuditLog_RequestRevisionReplay" ON public."AuditLog" USING btree ("TenantId", "ActorId", ((("MetadataJson" -> 'idempotency'::text) ->> 'keyHash'::text))) WHERE ((("Action")::text = 'REQUEST.REVISED'::text) AND ((("MetadataJson" -> 'idempotency'::text) ->> 'keyHash'::text) IS NOT NULL));


--
-- Name: UX_AuditLog_RequestReworkReplay; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "UX_AuditLog_RequestReworkReplay" ON public."AuditLog" USING btree ("TenantId", "ActorId", ((("MetadataJson" -> 'idempotency'::text) ->> 'keyHash'::text))) WHERE ((("Action")::text = 'REQUEST.REWORK'::text) AND ((("MetadataJson" -> 'idempotency'::text) ->> 'keyHash'::text) IS NOT NULL));


--
-- Name: UX_AuditLog_RequestRoutingReplay; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "UX_AuditLog_RequestRoutingReplay" ON public."AuditLog" USING btree ("TenantId", "ActorId", ((("MetadataJson" -> 'idempotency'::text) ->> 'keyHash'::text))) WHERE ((("Action")::text = 'REQUEST.ROUTED'::text) AND ((("MetadataJson" -> 'idempotency'::text) ->> 'keyHash'::text) IS NOT NULL));


--
-- Name: UX_AuditLog_RequestSubmissionReplay; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "UX_AuditLog_RequestSubmissionReplay" ON public."AuditLog" USING btree ("TenantId", "ActorId", ((("MetadataJson" -> 'idempotency'::text) ->> 'keyHash'::text))) WHERE ((("Action")::text = 'REQUEST.SUBMITTED'::text) AND ((("MetadataJson" -> 'idempotency'::text) ->> 'keyHash'::text) IS NOT NULL));


--
-- Name: UX_AuditLog_TaskAcceptanceReplay; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "UX_AuditLog_TaskAcceptanceReplay" ON public."AuditLog" USING btree ("TenantId", "ActorId", ((("MetadataJson" -> 'idempotency'::text) ->> 'keyHash'::text))) WHERE ((("Action")::text = 'TASK.ACCEPTED'::text) AND ((("MetadataJson" -> 'idempotency'::text) ->> 'keyHash'::text) IS NOT NULL));


--
-- Name: UX_AuditLog_TaskAssignmentReplay; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "UX_AuditLog_TaskAssignmentReplay" ON public."AuditLog" USING btree ("TenantId", "ActorId", ((("MetadataJson" -> 'idempotency'::text) ->> 'keyHash'::text))) WHERE ((("Action")::text = 'TASK.ASSIGNED'::text) AND ((("MetadataJson" -> 'idempotency'::text) ->> 'keyHash'::text) IS NOT NULL));


--
-- Name: UX_AuditLog_TaskCreationReplay; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "UX_AuditLog_TaskCreationReplay" ON public."AuditLog" USING btree ("TenantId", "ActorId", ((("MetadataJson" -> 'idempotency'::text) ->> 'keyHash'::text))) WHERE ((("Action")::text = 'TASK.CREATED'::text) AND ((("MetadataJson" -> 'idempotency'::text) ->> 'keyHash'::text) IS NOT NULL));


--
-- Name: UX_AuditLog_TaskExecutionReplay; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "UX_AuditLog_TaskExecutionReplay" ON public."AuditLog" USING btree ("TenantId", "ActorId", ((("MetadataJson" -> 'idempotency'::text) ->> 'keyHash'::text))) WHERE ((("Action")::text = 'TASK.STARTED'::text) AND ((("MetadataJson" -> 'idempotency'::text) ->> 'keyHash'::text) IS NOT NULL));


--
-- Name: UX_AuditLog_TaskResultConfirmationReplay; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "UX_AuditLog_TaskResultConfirmationReplay" ON public."AuditLog" USING btree ("TenantId", "ActorId", ((("MetadataJson" -> 'idempotency'::text) ->> 'keyHash'::text))) WHERE ((("Action")::text = ANY ((ARRAY['TASK.RESULT_CONFIRMED'::character varying, 'TASK.RESULT_REWORK'::character varying])::text[])) AND ((("MetadataJson" -> 'idempotency'::text) ->> 'keyHash'::text) IS NOT NULL));


--
-- Name: UX_AuditLog_TaskResultSubmissionReplay; Type: INDEX; Schema: public; Owner: -
--

CREATE UNIQUE INDEX "UX_AuditLog_TaskResultSubmissionReplay" ON public."AuditLog" USING btree ("TenantId", "ActorId", ((("MetadataJson" -> 'idempotency'::text) ->> 'keyHash'::text))) WHERE ((("Action")::text = 'TASK.RESULT_SUBMITTED'::text) AND ((("MetadataJson" -> 'idempotency'::text) ->> 'keyHash'::text) IS NOT NULL));


--
-- Name: AuditLog audit_no_mutation; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER audit_no_mutation BEFORE DELETE OR UPDATE ON public."AuditLog" FOR EACH ROW EXECUTE FUNCTION public.bizflow_audit_immutable();


--
-- Name: AuditLog audit_no_truncate; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER audit_no_truncate BEFORE TRUNCATE ON public."AuditLog" FOR EACH STATEMENT EXECUTE FUNCTION public.bizflow_audit_immutable();


--
-- Name: BusinessCalendar calendar_guard; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER calendar_guard BEFORE INSERT OR DELETE OR UPDATE ON public."BusinessCalendar" FOR EACH ROW EXECUTE FUNCTION public.bizflow_calendar_guard();


--
-- Name: Confirmation confirmation_guard; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER confirmation_guard BEFORE INSERT OR DELETE OR UPDATE ON public."Confirmation" FOR EACH ROW EXECUTE FUNCTION public.bizflow_confirmation_guard();


--
-- Name: Confirmation confirmation_truncate_guard; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER confirmation_truncate_guard BEFORE TRUNCATE ON public."Confirmation" FOR EACH STATEMENT EXECUTE FUNCTION public.bizflow_confirmation_guard();


--
-- Name: Department department_tenant_immutable; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER department_tenant_immutable BEFORE UPDATE ON public."Department" FOR EACH ROW EXECUTE FUNCTION public.bizflow_tenant_immutable();


--
-- Name: ManagementScope management_scope_boundary; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER management_scope_boundary BEFORE INSERT OR UPDATE ON public."ManagementScope" FOR EACH ROW EXECUTE FUNCTION public.bizflow_management_scope_boundary();


--
-- Name: Notification notification_boundary; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER notification_boundary BEFORE INSERT OR UPDATE ON public."Notification" FOR EACH ROW EXECUTE FUNCTION public.bizflow_notification_boundary();


--
-- Name: Request request_guard; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER request_guard BEFORE INSERT OR DELETE OR UPDATE ON public."Request" FOR EACH ROW EXECUTE FUNCTION public.bizflow_request_guard();


--
-- Name: RequestResolution request_resolution_guard; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER request_resolution_guard BEFORE INSERT OR DELETE OR UPDATE ON public."RequestResolution" FOR EACH ROW EXECUTE FUNCTION public.bizflow_request_resolution_guard();


--
-- Name: RequestResolution request_resolution_truncate_guard; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER request_resolution_truncate_guard BEFORE TRUNCATE ON public."RequestResolution" FOR EACH STATEMENT EXECUTE FUNCTION public.bizflow_request_resolution_guard();


--
-- Name: RolePermission role_permission_scope; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER role_permission_scope BEFORE INSERT OR UPDATE ON public."RolePermission" FOR EACH ROW EXECUTE FUNCTION public.bizflow_role_permission_scope();


--
-- Name: Role role_tenant_immutable; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER role_tenant_immutable BEFORE UPDATE ON public."Role" FOR EACH ROW EXECUTE FUNCTION public.bizflow_tenant_immutable();


--
-- Name: ServiceCategory service_category_guard; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER service_category_guard BEFORE INSERT OR UPDATE ON public."ServiceCategory" FOR EACH ROW EXECUTE FUNCTION public.bizflow_service_category_guard();


--
-- Name: Service service_guard; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER service_guard BEFORE INSERT OR UPDATE ON public."Service" FOR EACH ROW EXECUTE FUNCTION public.bizflow_service_guard();


--
-- Name: SLAProfile sla_profile_identity; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER sla_profile_identity BEFORE UPDATE ON public."SLAProfile" FOR EACH ROW EXECUTE FUNCTION public.bizflow_sla_profile_identity();


--
-- Name: SLAVersion sla_version_immutable; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER sla_version_immutable BEFORE DELETE OR UPDATE ON public."SLAVersion" FOR EACH ROW EXECUTE FUNCTION public.bizflow_sla_version_immutable();


--
-- Name: SLAVersion sla_version_references; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER sla_version_references BEFORE INSERT ON public."SLAVersion" FOR EACH ROW EXECUTE FUNCTION public.bizflow_sla_version_references();


--
-- Name: TaskAssignment task_assignment_guard; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER task_assignment_guard BEFORE INSERT OR DELETE OR UPDATE ON public."TaskAssignment" FOR EACH ROW EXECUTE FUNCTION public.bizflow_task_assignment_guard();


--
-- Name: TaskAssignment task_assignment_truncate_guard; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER task_assignment_truncate_guard BEFORE TRUNCATE ON public."TaskAssignment" FOR EACH STATEMENT EXECUTE FUNCTION public.bizflow_task_assignment_guard();


--
-- Name: TaskChecklistItem task_checklist_guard; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER task_checklist_guard BEFORE INSERT OR DELETE OR UPDATE ON public."TaskChecklistItem" FOR EACH ROW EXECUTE FUNCTION public.bizflow_task_checklist_guard();


--
-- Name: TaskChecklistItem task_checklist_truncate_guard; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER task_checklist_truncate_guard BEFORE TRUNCATE ON public."TaskChecklistItem" FOR EACH STATEMENT EXECUTE FUNCTION public.bizflow_task_checklist_guard();


--
-- Name: Task task_guard; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER task_guard BEFORE INSERT OR DELETE OR UPDATE ON public."Task" FOR EACH ROW EXECUTE FUNCTION public.bizflow_task_guard();


--
-- Name: TaskProgressReport task_progress_guard; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER task_progress_guard BEFORE INSERT OR DELETE OR UPDATE ON public."TaskProgressReport" FOR EACH ROW EXECUTE FUNCTION public.bizflow_task_evidence_guard();


--
-- Name: TaskProgressReport task_progress_truncate_guard; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER task_progress_truncate_guard BEFORE TRUNCATE ON public."TaskProgressReport" FOR EACH STATEMENT EXECUTE FUNCTION public.bizflow_task_evidence_guard();


--
-- Name: TaskResult task_result_guard; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER task_result_guard BEFORE INSERT OR DELETE OR UPDATE ON public."TaskResult" FOR EACH ROW EXECUTE FUNCTION public.bizflow_task_evidence_guard();


--
-- Name: TaskResult task_result_truncate_guard; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER task_result_truncate_guard BEFORE TRUNCATE ON public."TaskResult" FOR EACH STATEMENT EXECUTE FUNCTION public.bizflow_task_evidence_guard();


--
-- Name: Task task_truncate_guard; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER task_truncate_guard BEFORE TRUNCATE ON public."Task" FOR EACH STATEMENT EXECUTE FUNCTION public.bizflow_task_guard();


--
-- Name: TenantSetting tenant_setting_boundary; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER tenant_setting_boundary BEFORE INSERT OR UPDATE ON public."TenantSetting" FOR EACH ROW EXECUTE FUNCTION public.bizflow_tenant_setting_boundary();


--
-- Name: Attachment trg_bizflow_attachment_guard; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER trg_bizflow_attachment_guard BEFORE INSERT OR UPDATE ON public."Attachment" FOR EACH ROW EXECUTE FUNCTION public.bizflow_attachment_guard();


--
-- Name: Comment trg_bizflow_comment_guard; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER trg_bizflow_comment_guard BEFORE INSERT OR UPDATE ON public."Comment" FOR EACH ROW EXECUTE FUNCTION public.bizflow_comment_guard();


--
-- Name: UserRole user_role_scope; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER user_role_scope BEFORE INSERT OR UPDATE ON public."UserRole" FOR EACH ROW EXECUTE FUNCTION public.bizflow_user_role_scope();


--
-- Name: User user_tenant_immutable; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER user_tenant_immutable BEFORE UPDATE ON public."User" FOR EACH ROW EXECUTE FUNCTION public.bizflow_tenant_immutable();


--
-- Name: Workflow workflow_identity; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER workflow_identity BEFORE UPDATE ON public."Workflow" FOR EACH ROW EXECUTE FUNCTION public.bizflow_workflow_identity();


--
-- Name: Workflow workflow_no_truncate; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER workflow_no_truncate BEFORE TRUNCATE ON public."Workflow" FOR EACH STATEMENT EXECUTE FUNCTION public.bizflow_workflow_no_truncate();


--
-- Name: WorkflowStep workflow_step_immutable; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER workflow_step_immutable BEFORE INSERT OR DELETE OR UPDATE ON public."WorkflowStep" FOR EACH ROW EXECUTE FUNCTION public.bizflow_workflow_child_immutable();


--
-- Name: WorkflowStep workflow_step_no_truncate; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER workflow_step_no_truncate BEFORE TRUNCATE ON public."WorkflowStep" FOR EACH STATEMENT EXECUTE FUNCTION public.bizflow_workflow_no_truncate();


--
-- Name: WorkflowTransition workflow_transition_immutable; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER workflow_transition_immutable BEFORE INSERT OR DELETE OR UPDATE ON public."WorkflowTransition" FOR EACH ROW EXECUTE FUNCTION public.bizflow_workflow_child_immutable();


--
-- Name: WorkflowTransition workflow_transition_no_truncate; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER workflow_transition_no_truncate BEFORE TRUNCATE ON public."WorkflowTransition" FOR EACH STATEMENT EXECUTE FUNCTION public.bizflow_workflow_no_truncate();


--
-- Name: WorkflowVersion workflow_version_immutable; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER workflow_version_immutable BEFORE DELETE OR UPDATE ON public."WorkflowVersion" FOR EACH ROW EXECUTE FUNCTION public.bizflow_workflow_version_immutable();


--
-- Name: WorkflowVersion workflow_version_no_truncate; Type: TRIGGER; Schema: public; Owner: -
--

CREATE TRIGGER workflow_version_no_truncate BEFORE TRUNCATE ON public."WorkflowVersion" FOR EACH STATEMENT EXECUTE FUNCTION public.bizflow_workflow_no_truncate();


--
-- Name: AIAgentAction FK_AIAgentAction_AIInteraction_AIInteractionId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."AIAgentAction"
    ADD CONSTRAINT "FK_AIAgentAction_AIInteraction_AIInteractionId" FOREIGN KEY ("AIInteractionId") REFERENCES public."AIInteraction"("AIInteractionId") ON DELETE CASCADE;


--
-- Name: AIAgentAction FK_AIAgentAction_Tenant_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."AIAgentAction"
    ADD CONSTRAINT "FK_AIAgentAction_Tenant_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Tenant"("TenantId") ON DELETE RESTRICT;


--
-- Name: AIInteraction FK_AIInteraction_Tenant_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."AIInteraction"
    ADD CONSTRAINT "FK_AIInteraction_Tenant_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Tenant"("TenantId") ON DELETE RESTRICT;


--
-- Name: AIInteraction FK_AIInteraction_User_UserId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."AIInteraction"
    ADD CONSTRAINT "FK_AIInteraction_User_UserId" FOREIGN KEY ("UserId") REFERENCES public."User"("UserId") ON DELETE RESTRICT;


--
-- Name: AIRecommendation FK_AIRecommendation_AIInteraction_AIInteractionId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."AIRecommendation"
    ADD CONSTRAINT "FK_AIRecommendation_AIInteraction_AIInteractionId" FOREIGN KEY ("AIInteractionId") REFERENCES public."AIInteraction"("AIInteractionId") ON DELETE CASCADE;


--
-- Name: AIRecommendation FK_AIRecommendation_Tenant_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."AIRecommendation"
    ADD CONSTRAINT "FK_AIRecommendation_Tenant_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Tenant"("TenantId") ON DELETE RESTRICT;


--
-- Name: AIRecommendation FK_AIRecommendation_User_DecisionBy; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."AIRecommendation"
    ADD CONSTRAINT "FK_AIRecommendation_User_DecisionBy" FOREIGN KEY ("DecisionBy") REFERENCES public."User"("UserId") ON DELETE RESTRICT;


--
-- Name: AIVectorMemory FK_AIVectorMemory_Tenant_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."AIVectorMemory"
    ADD CONSTRAINT "FK_AIVectorMemory_Tenant_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Tenant"("TenantId") ON DELETE CASCADE;


--
-- Name: Attachment FK_Attachment_Tenant_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Attachment"
    ADD CONSTRAINT "FK_Attachment_Tenant_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Tenant"("TenantId") ON DELETE RESTRICT;


--
-- Name: Attachment FK_Attachment_User_UploadedBy; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Attachment"
    ADD CONSTRAINT "FK_Attachment_User_UploadedBy" FOREIGN KEY ("UploadedBy") REFERENCES public."User"("UserId") ON DELETE RESTRICT;


--
-- Name: AuditLog FK_AuditLog_Tenant_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."AuditLog"
    ADD CONSTRAINT "FK_AuditLog_Tenant_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Tenant"("TenantId") ON DELETE RESTRICT;


--
-- Name: AuthenticationSession FK_AuthenticationSession_AuthenticationSession_ReplacedById; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."AuthenticationSession"
    ADD CONSTRAINT "FK_AuthenticationSession_AuthenticationSession_ReplacedById" FOREIGN KEY ("ReplacedById") REFERENCES public."AuthenticationSession"("AuthenticationSessionId") ON DELETE RESTRICT;


--
-- Name: AuthenticationSession FK_AuthenticationSession_User_UserId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."AuthenticationSession"
    ADD CONSTRAINT "FK_AuthenticationSession_User_UserId" FOREIGN KEY ("UserId") REFERENCES public."User"("UserId") ON DELETE RESTRICT;


--
-- Name: BusinessCalendar FK_BusinessCalendar_Tenant_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."BusinessCalendar"
    ADD CONSTRAINT "FK_BusinessCalendar_Tenant_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Tenant"("TenantId") ON DELETE RESTRICT;


--
-- Name: Comment FK_Comment_Tenant_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Comment"
    ADD CONSTRAINT "FK_Comment_Tenant_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Tenant"("TenantId") ON DELETE RESTRICT;


--
-- Name: Comment FK_Comment_User_AuthorId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Comment"
    ADD CONSTRAINT "FK_Comment_User_AuthorId" FOREIGN KEY ("AuthorId") REFERENCES public."User"("UserId") ON DELETE RESTRICT;


--
-- Name: Confirmation FK_Confirmation_Tenant_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Confirmation"
    ADD CONSTRAINT "FK_Confirmation_Tenant_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Tenant"("TenantId") ON DELETE RESTRICT;


--
-- Name: Confirmation FK_Confirmation_User_ActorId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Confirmation"
    ADD CONSTRAINT "FK_Confirmation_User_ActorId" FOREIGN KEY ("ActorId") REFERENCES public."User"("UserId") ON DELETE RESTRICT;


--
-- Name: DeadlineReminderSchedule FK_DeadlineReminderSchedule_Task_TaskId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."DeadlineReminderSchedule"
    ADD CONSTRAINT "FK_DeadlineReminderSchedule_Task_TaskId" FOREIGN KEY ("TaskId") REFERENCES public."Task"("TaskId") ON DELETE RESTRICT;


--
-- Name: DeadlineReminderSchedule FK_DeadlineReminderSchedule_Tenant_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."DeadlineReminderSchedule"
    ADD CONSTRAINT "FK_DeadlineReminderSchedule_Tenant_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Tenant"("TenantId") ON DELETE RESTRICT;


--
-- Name: DeadlineReminderSchedule FK_DeadlineReminderSchedule_User_UserId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."DeadlineReminderSchedule"
    ADD CONSTRAINT "FK_DeadlineReminderSchedule_User_UserId" FOREIGN KEY ("UserId") REFERENCES public."User"("UserId") ON DELETE RESTRICT;


--
-- Name: Department FK_Department_Department_TenantId_ParentDepartmentId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Department"
    ADD CONSTRAINT "FK_Department_Department_TenantId_ParentDepartmentId" FOREIGN KEY ("TenantId", "ParentDepartmentId") REFERENCES public."Department"("TenantId", "DepartmentId") ON DELETE RESTRICT;


--
-- Name: Department FK_Department_Tenant_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Department"
    ADD CONSTRAINT "FK_Department_Tenant_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Tenant"("TenantId") ON DELETE RESTRICT;


--
-- Name: ManagementScope FK_ManagementScope_Department_TenantId_DepartmentId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ManagementScope"
    ADD CONSTRAINT "FK_ManagementScope_Department_TenantId_DepartmentId" FOREIGN KEY ("TenantId", "DepartmentId") REFERENCES public."Department"("TenantId", "DepartmentId") ON DELETE RESTRICT;


--
-- Name: ManagementScope FK_ManagementScope_Tenant_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ManagementScope"
    ADD CONSTRAINT "FK_ManagementScope_Tenant_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Tenant"("TenantId") ON DELETE RESTRICT;


--
-- Name: ManagementScope FK_ManagementScope_User_CreatedBy; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ManagementScope"
    ADD CONSTRAINT "FK_ManagementScope_User_CreatedBy" FOREIGN KEY ("CreatedBy") REFERENCES public."User"("UserId") ON DELETE RESTRICT;


--
-- Name: ManagementScope FK_ManagementScope_User_UserId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ManagementScope"
    ADD CONSTRAINT "FK_ManagementScope_User_UserId" FOREIGN KEY ("UserId") REFERENCES public."User"("UserId") ON DELETE RESTRICT;


--
-- Name: NotificationLog FK_NotificationLog_Task_TaskId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."NotificationLog"
    ADD CONSTRAINT "FK_NotificationLog_Task_TaskId" FOREIGN KEY ("TaskId") REFERENCES public."Task"("TaskId") ON DELETE RESTRICT;


--
-- Name: NotificationLog FK_NotificationLog_Tenant_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."NotificationLog"
    ADD CONSTRAINT "FK_NotificationLog_Tenant_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Tenant"("TenantId") ON DELETE RESTRICT;


--
-- Name: NotificationLog FK_NotificationLog_User_UserId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."NotificationLog"
    ADD CONSTRAINT "FK_NotificationLog_User_UserId" FOREIGN KEY ("UserId") REFERENCES public."User"("UserId") ON DELETE RESTRICT;


--
-- Name: Notification FK_Notification_Tenant_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Notification"
    ADD CONSTRAINT "FK_Notification_Tenant_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Tenant"("TenantId") ON DELETE RESTRICT;


--
-- Name: Notification FK_Notification_User_RecipientId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Notification"
    ADD CONSTRAINT "FK_Notification_User_RecipientId" FOREIGN KEY ("RecipientId") REFERENCES public."User"("UserId") ON DELETE RESTRICT;


--
-- Name: RequestResolution FK_RequestResolution_Request_RequestId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."RequestResolution"
    ADD CONSTRAINT "FK_RequestResolution_Request_RequestId" FOREIGN KEY ("RequestId") REFERENCES public."Request"("RequestId") ON DELETE RESTRICT;


--
-- Name: RequestResolution FK_RequestResolution_User_ResolverId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."RequestResolution"
    ADD CONSTRAINT "FK_RequestResolution_User_ResolverId" FOREIGN KEY ("ResolverId") REFERENCES public."User"("UserId") ON DELETE RESTRICT;


--
-- Name: RequestRouting FK_RequestRouting_Department_FromDepartmentId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."RequestRouting"
    ADD CONSTRAINT "FK_RequestRouting_Department_FromDepartmentId" FOREIGN KEY ("FromDepartmentId") REFERENCES public."Department"("DepartmentId") ON DELETE RESTRICT;


--
-- Name: RequestRouting FK_RequestRouting_Department_ToDepartmentId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."RequestRouting"
    ADD CONSTRAINT "FK_RequestRouting_Department_ToDepartmentId" FOREIGN KEY ("ToDepartmentId") REFERENCES public."Department"("DepartmentId") ON DELETE RESTRICT;


--
-- Name: RequestRouting FK_RequestRouting_Request_RequestId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."RequestRouting"
    ADD CONSTRAINT "FK_RequestRouting_Request_RequestId" FOREIGN KEY ("RequestId") REFERENCES public."Request"("RequestId") ON DELETE RESTRICT;


--
-- Name: RequestRouting FK_RequestRouting_Tenant_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."RequestRouting"
    ADD CONSTRAINT "FK_RequestRouting_Tenant_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Tenant"("TenantId") ON DELETE RESTRICT;


--
-- Name: RequestRouting FK_RequestRouting_User_FromUserId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."RequestRouting"
    ADD CONSTRAINT "FK_RequestRouting_User_FromUserId" FOREIGN KEY ("FromUserId") REFERENCES public."User"("UserId") ON DELETE RESTRICT;


--
-- Name: RequestRouting FK_RequestRouting_User_RoutedBy; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."RequestRouting"
    ADD CONSTRAINT "FK_RequestRouting_User_RoutedBy" FOREIGN KEY ("RoutedBy") REFERENCES public."User"("UserId") ON DELETE RESTRICT;


--
-- Name: RequestRouting FK_RequestRouting_User_ToUserId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."RequestRouting"
    ADD CONSTRAINT "FK_RequestRouting_User_ToUserId" FOREIGN KEY ("ToUserId") REFERENCES public."User"("UserId") ON DELETE RESTRICT;


--
-- Name: Request FK_Request_Request_ParentRequestId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Request"
    ADD CONSTRAINT "FK_Request_Request_ParentRequestId" FOREIGN KEY ("ParentRequestId") REFERENCES public."Request"("RequestId") ON DELETE RESTRICT;


--
-- Name: Request FK_Request_Request_RevisedFromRequestId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Request"
    ADD CONSTRAINT "FK_Request_Request_RevisedFromRequestId" FOREIGN KEY ("RevisedFromRequestId") REFERENCES public."Request"("RequestId") ON DELETE RESTRICT;


--
-- Name: Request FK_Request_SLAVersion_SLAVersionId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Request"
    ADD CONSTRAINT "FK_Request_SLAVersion_SLAVersionId" FOREIGN KEY ("SLAVersionId") REFERENCES public."SLAVersion"("SLAVersionId") ON DELETE RESTRICT;


--
-- Name: Request FK_Request_ServiceCategory_CategoryId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Request"
    ADD CONSTRAINT "FK_Request_ServiceCategory_CategoryId" FOREIGN KEY ("CategoryId") REFERENCES public."ServiceCategory"("ServiceCategoryId") ON DELETE RESTRICT;


--
-- Name: Request FK_Request_Service_ServiceId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Request"
    ADD CONSTRAINT "FK_Request_Service_ServiceId" FOREIGN KEY ("ServiceId") REFERENCES public."Service"("ServiceId") ON DELETE RESTRICT;


--
-- Name: Request FK_Request_Tenant_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Request"
    ADD CONSTRAINT "FK_Request_Tenant_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Tenant"("TenantId") ON DELETE RESTRICT;


--
-- Name: Request FK_Request_User_RequesterId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Request"
    ADD CONSTRAINT "FK_Request_User_RequesterId" FOREIGN KEY ("RequesterId") REFERENCES public."User"("UserId") ON DELETE RESTRICT;


--
-- Name: Request FK_Request_WorkflowVersion_WorkflowVersionId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Request"
    ADD CONSTRAINT "FK_Request_WorkflowVersion_WorkflowVersionId" FOREIGN KEY ("WorkflowVersionId") REFERENCES public."WorkflowVersion"("WorkflowVersionId") ON DELETE RESTRICT;


--
-- Name: RolePermission FK_RolePermission_Permission_PermissionId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."RolePermission"
    ADD CONSTRAINT "FK_RolePermission_Permission_PermissionId" FOREIGN KEY ("PermissionId") REFERENCES public."Permission"("PermissionId") ON DELETE RESTRICT;


--
-- Name: RolePermission FK_RolePermission_Role_RoleId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."RolePermission"
    ADD CONSTRAINT "FK_RolePermission_Role_RoleId" FOREIGN KEY ("RoleId") REFERENCES public."Role"("RoleId") ON DELETE RESTRICT;


--
-- Name: Role FK_Role_Tenant_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Role"
    ADD CONSTRAINT "FK_Role_Tenant_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Tenant"("TenantId") ON DELETE RESTRICT;


--
-- Name: SLAProfile FK_SLAProfile_Tenant_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."SLAProfile"
    ADD CONSTRAINT "FK_SLAProfile_Tenant_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Tenant"("TenantId") ON DELETE RESTRICT;


--
-- Name: SLAVersion FK_SLAVersion_BusinessCalendar_CalendarId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."SLAVersion"
    ADD CONSTRAINT "FK_SLAVersion_BusinessCalendar_CalendarId" FOREIGN KEY ("CalendarId") REFERENCES public."BusinessCalendar"("CalendarId") ON DELETE RESTRICT;


--
-- Name: SLAVersion FK_SLAVersion_SLAProfile_SLAProfileId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."SLAVersion"
    ADD CONSTRAINT "FK_SLAVersion_SLAProfile_SLAProfileId" FOREIGN KEY ("SLAProfileId") REFERENCES public."SLAProfile"("SLAProfileId") ON DELETE RESTRICT;


--
-- Name: ServiceCategory FK_ServiceCategory_Service_ServiceId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."ServiceCategory"
    ADD CONSTRAINT "FK_ServiceCategory_Service_ServiceId" FOREIGN KEY ("ServiceId") REFERENCES public."Service"("ServiceId") ON DELETE RESTRICT;


--
-- Name: Service FK_Service_SLAVersion_ActiveSLAVersionId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Service"
    ADD CONSTRAINT "FK_Service_SLAVersion_ActiveSLAVersionId" FOREIGN KEY ("ActiveSLAVersionId") REFERENCES public."SLAVersion"("SLAVersionId") ON DELETE RESTRICT;


--
-- Name: Service FK_Service_Tenant_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Service"
    ADD CONSTRAINT "FK_Service_Tenant_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Tenant"("TenantId") ON DELETE RESTRICT;


--
-- Name: Service FK_Service_WorkflowVersion_ActiveWorkflowVersionId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Service"
    ADD CONSTRAINT "FK_Service_WorkflowVersion_ActiveWorkflowVersionId" FOREIGN KEY ("ActiveWorkflowVersionId") REFERENCES public."WorkflowVersion"("WorkflowVersionId") ON DELETE RESTRICT;


--
-- Name: TaskAssignment FK_TaskAssignment_Department_DepartmentId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."TaskAssignment"
    ADD CONSTRAINT "FK_TaskAssignment_Department_DepartmentId" FOREIGN KEY ("DepartmentId") REFERENCES public."Department"("DepartmentId") ON DELETE RESTRICT;


--
-- Name: TaskAssignment FK_TaskAssignment_Task_TaskId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."TaskAssignment"
    ADD CONSTRAINT "FK_TaskAssignment_Task_TaskId" FOREIGN KEY ("TaskId") REFERENCES public."Task"("TaskId") ON DELETE RESTRICT;


--
-- Name: TaskAssignment FK_TaskAssignment_User_AssignedBy; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."TaskAssignment"
    ADD CONSTRAINT "FK_TaskAssignment_User_AssignedBy" FOREIGN KEY ("AssignedBy") REFERENCES public."User"("UserId") ON DELETE RESTRICT;


--
-- Name: TaskAssignment FK_TaskAssignment_User_UserId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."TaskAssignment"
    ADD CONSTRAINT "FK_TaskAssignment_User_UserId" FOREIGN KEY ("UserId") REFERENCES public."User"("UserId") ON DELETE RESTRICT;


--
-- Name: TaskChecklistItem FK_TaskChecklistItem_Task_TaskId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."TaskChecklistItem"
    ADD CONSTRAINT "FK_TaskChecklistItem_Task_TaskId" FOREIGN KEY ("TaskId") REFERENCES public."Task"("TaskId") ON DELETE RESTRICT;


--
-- Name: TaskChecklistItem FK_TaskChecklistItem_User_CompletedBy; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."TaskChecklistItem"
    ADD CONSTRAINT "FK_TaskChecklistItem_User_CompletedBy" FOREIGN KEY ("CompletedBy") REFERENCES public."User"("UserId") ON DELETE RESTRICT;


--
-- Name: TaskProgressReport FK_TaskProgressReport_Task_TaskId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."TaskProgressReport"
    ADD CONSTRAINT "FK_TaskProgressReport_Task_TaskId" FOREIGN KEY ("TaskId") REFERENCES public."Task"("TaskId") ON DELETE RESTRICT;


--
-- Name: TaskProgressReport FK_TaskProgressReport_User_AuthorId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."TaskProgressReport"
    ADD CONSTRAINT "FK_TaskProgressReport_User_AuthorId" FOREIGN KEY ("AuthorId") REFERENCES public."User"("UserId") ON DELETE RESTRICT;


--
-- Name: TaskResult FK_TaskResult_Task_TaskId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."TaskResult"
    ADD CONSTRAINT "FK_TaskResult_Task_TaskId" FOREIGN KEY ("TaskId") REFERENCES public."Task"("TaskId") ON DELETE RESTRICT;


--
-- Name: TaskResult FK_TaskResult_User_AuthorId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."TaskResult"
    ADD CONSTRAINT "FK_TaskResult_User_AuthorId" FOREIGN KEY ("AuthorId") REFERENCES public."User"("UserId") ON DELETE RESTRICT;


--
-- Name: Task FK_Task_Request_RequestId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Task"
    ADD CONSTRAINT "FK_Task_Request_RequestId" FOREIGN KEY ("RequestId") REFERENCES public."Request"("RequestId") ON DELETE RESTRICT;


--
-- Name: Task FK_Task_SLAVersion_SLAVersionId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Task"
    ADD CONSTRAINT "FK_Task_SLAVersion_SLAVersionId" FOREIGN KEY ("SLAVersionId") REFERENCES public."SLAVersion"("SLAVersionId") ON DELETE RESTRICT;


--
-- Name: Task FK_Task_Tenant_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Task"
    ADD CONSTRAINT "FK_Task_Tenant_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Tenant"("TenantId") ON DELETE RESTRICT;


--
-- Name: Task FK_Task_User_CreatorId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Task"
    ADD CONSTRAINT "FK_Task_User_CreatorId" FOREIGN KEY ("CreatorId") REFERENCES public."User"("UserId") ON DELETE RESTRICT;


--
-- Name: Task FK_Task_WorkflowVersion_WorkflowVersionId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Task"
    ADD CONSTRAINT "FK_Task_WorkflowVersion_WorkflowVersionId" FOREIGN KEY ("WorkflowVersionId") REFERENCES public."WorkflowVersion"("WorkflowVersionId") ON DELETE RESTRICT;


--
-- Name: TenantSetting FK_TenantSetting_Tenant_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."TenantSetting"
    ADD CONSTRAINT "FK_TenantSetting_Tenant_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Tenant"("TenantId") ON DELETE RESTRICT;


--
-- Name: TenantSetting FK_TenantSetting_User_UpdatedBy; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."TenantSetting"
    ADD CONSTRAINT "FK_TenantSetting_User_UpdatedBy" FOREIGN KEY ("UpdatedBy") REFERENCES public."User"("UserId") ON DELETE RESTRICT;


--
-- Name: Tenant FK_Tenant_Company_CompanyId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Tenant"
    ADD CONSTRAINT "FK_Tenant_Company_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES public."Company"("CompanyId") ON DELETE RESTRICT;


--
-- Name: UserRole FK_UserRole_Role_RoleId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."UserRole"
    ADD CONSTRAINT "FK_UserRole_Role_RoleId" FOREIGN KEY ("RoleId") REFERENCES public."Role"("RoleId") ON DELETE RESTRICT;


--
-- Name: UserRole FK_UserRole_User_UserId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."UserRole"
    ADD CONSTRAINT "FK_UserRole_User_UserId" FOREIGN KEY ("UserId") REFERENCES public."User"("UserId") ON DELETE RESTRICT;


--
-- Name: User FK_User_Department_TenantId_DepartmentId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."User"
    ADD CONSTRAINT "FK_User_Department_TenantId_DepartmentId" FOREIGN KEY ("TenantId", "DepartmentId") REFERENCES public."Department"("TenantId", "DepartmentId") ON DELETE RESTRICT;


--
-- Name: User FK_User_Tenant_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."User"
    ADD CONSTRAINT "FK_User_Tenant_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Tenant"("TenantId") ON DELETE RESTRICT;


--
-- Name: WebPushSubscription FK_WebPushSubscription_Tenant_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."WebPushSubscription"
    ADD CONSTRAINT "FK_WebPushSubscription_Tenant_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Tenant"("TenantId") ON DELETE RESTRICT;


--
-- Name: WebPushSubscription FK_WebPushSubscription_User_UserId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."WebPushSubscription"
    ADD CONSTRAINT "FK_WebPushSubscription_User_UserId" FOREIGN KEY ("UserId") REFERENCES public."User"("UserId") ON DELETE RESTRICT;


--
-- Name: WorkflowStep FK_WorkflowStep_WorkflowVersion_WorkflowVersionId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."WorkflowStep"
    ADD CONSTRAINT "FK_WorkflowStep_WorkflowVersion_WorkflowVersionId" FOREIGN KEY ("WorkflowVersionId") REFERENCES public."WorkflowVersion"("WorkflowVersionId") ON DELETE RESTRICT;


--
-- Name: WorkflowTransition FK_WorkflowTransition_WorkflowVersion_WorkflowVersionId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."WorkflowTransition"
    ADD CONSTRAINT "FK_WorkflowTransition_WorkflowVersion_WorkflowVersionId" FOREIGN KEY ("WorkflowVersionId") REFERENCES public."WorkflowVersion"("WorkflowVersionId") ON DELETE RESTRICT;


--
-- Name: WorkflowVersion FK_WorkflowVersion_Workflow_WorkflowId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."WorkflowVersion"
    ADD CONSTRAINT "FK_WorkflowVersion_Workflow_WorkflowId" FOREIGN KEY ("WorkflowId") REFERENCES public."Workflow"("WorkflowId") ON DELETE RESTRICT;


--
-- Name: Workflow FK_Workflow_Tenant_TenantId; Type: FK CONSTRAINT; Schema: public; Owner: -
--

ALTER TABLE ONLY public."Workflow"
    ADD CONSTRAINT "FK_Workflow_Tenant_TenantId" FOREIGN KEY ("TenantId") REFERENCES public."Tenant"("TenantId") ON DELETE RESTRICT;


--
-- PostgreSQL database dump complete
--

\unrestrict r5KTmKCRf2LmYe43kXW0CiGwe0H4MkTvtfUUOouuyODogywdj9u1qk7wOnv84lv

