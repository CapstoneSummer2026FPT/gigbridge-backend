import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router';
import {
  AlertCircle, ArrowLeft, CheckCircle2, ChevronDown, Clock, Download,
  ExternalLink, FileText, RotateCcw, ShieldCheck, Wallet,
} from 'lucide-react';
import { AppLayout } from '../../../shared/components/AppLayout';
import { GigCoinLogo } from '../../../shared/components/GigCoinAmount';
import { contractGetAPI } from '../../../api/contractAPI/GET';
import { contractPostAPI } from '../../../api/contractAPI/POST';
import type { ContractDto, Milestone, MilestoneAttachment } from '../../../types/models/Contract';
import { MilestoneStatus } from '../../../types/models/Contract';
import {
  canApproveMilestone, formatContractAmount, formatContractDate, getMilestoneStatusLabel,
} from '../../../shared/utils/contractUtils';
import '../styles/approve-milestone-screen.css';

interface MilestoneWithAttachments extends Milestone {
  attachments?: MilestoneAttachment[];
  deliverableDescription?: string;
}

const NOTES_LIMIT = 500;

export default function ApproveMilestoneScreen() {
  const { contractId, milestoneId } = useParams<{ contractId: string; milestoneId: string }>();
  const navigate = useNavigate();
  const [contract, setContract] = useState<ContractDto | null>(null);
  const [milestone, setMilestone] = useState<MilestoneWithAttachments | null>(null);
  const [attachments, setAttachments] = useState<MilestoneAttachment[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);
  const [approvalAction, setApprovalAction] = useState<'pending' | 'approve' | 'reject'>('pending');
  const [approvalNotes, setApprovalNotes] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [showEscrowInfo, setShowEscrowInfo] = useState(true);

  useEffect(() => {
    const loadData = async () => {
      if (!contractId || !milestoneId) {
        setError('Missing contract or milestone ID');
        setLoading(false);
        return;
      }
      try {
        setLoading(true);
        setError(null);
        const contractResponse = await contractGetAPI.getContractById(contractId);
        if (!contractResponse.success || !contractResponse.data) {
          throw new Error(contractResponse.message || 'Failed to load contract');
        }
        setContract(contractResponse.data);
        const milestoneResponse = await contractGetAPI.getMilestoneById(milestoneId);
        if (!milestoneResponse.success || !milestoneResponse.data) {
          throw new Error(milestoneResponse.message || 'Failed to load milestone');
        }
        setMilestone(milestoneResponse.data);
        const attachmentsResponse = await contractGetAPI.getMilestoneAttachments(milestoneId);
        if (attachmentsResponse.success && attachmentsResponse.data) setAttachments(attachmentsResponse.data);
      } catch (caughtError) {
        setError(caughtError instanceof Error ? caughtError.message : 'An error occurred');
      } finally {
        setLoading(false);
      }
    };
    loadData();
  }, [contractId, milestoneId]);

  const handleApprove = async () => {
    if (!milestone || !contractId) return;
    try {
      setIsSubmitting(true);
      setError(null);
      const response = await contractPostAPI.approveMilestone(contractId, milestone.id);
      if (!response.success) throw new Error(response.message || 'Failed to approve milestone.');
      setMilestone({ ...milestone, status: MilestoneStatus.Approved });
      setApprovalAction('pending');
      setSuccessMessage('Milestone approved successfully. Returning to the workspace...');
      setTimeout(() => navigate(`/workspace/${contractId}`), 2000);
    } catch (caughtError) {
      setError(caughtError instanceof Error ? caughtError.message : 'An error occurred');
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleReject = async () => {
    if (!milestone || !contractId) return;
    try {
      setIsSubmitting(true);
      setError(null);
      const response = await contractPostAPI.requestMilestoneRevision(contractId, milestone.id);
      if (!response.success) throw new Error(response.message || 'Failed to request revisions.');
      setMilestone({ ...milestone, status: MilestoneStatus.InProgress });
      setApprovalAction('pending');
      setApprovalNotes('');
      setSuccessMessage('Revision requested. The freelancer can now update the deliverables.');
    } catch (caughtError) {
      setError(caughtError instanceof Error ? caughtError.message : 'An error occurred');
    } finally {
      setIsSubmitting(false);
    }
  };

  if (loading) return (
    <AppLayout><div className="approve-milestone-wrapper">
      <div className="approve-milestone-state" role="status" aria-live="polite">
        <span className="approve-milestone-spinner" aria-hidden="true" />
        <h1>Preparing your review</h1><p>Loading the milestone and submitted deliverables...</p>
      </div>
    </div></AppLayout>
  );

  if (!contract || !milestone) return (
    <AppLayout><div className="approve-milestone-wrapper">
      <div className="approve-milestone-state approve-milestone-state--error" role="alert">
        <AlertCircle size={48} /><h1>Unable to open this milestone</h1>
        <p>{error || 'The milestone or contract could not be found.'}</p>
        <button onClick={() => navigate(-1)} className="approve-milestone-secondary-button">
          <ArrowLeft size={18} /> Go back
        </button>
      </div>
    </div></AppLayout>
  );

  const canApprove = canApproveMilestone(milestone.status);
  const isApproved = milestone.status === MilestoneStatus.Approved;
  const isPaid = milestone.status === MilestoneStatus.PaymentConfirmed;
  const notesTooLong = approvalNotes.length > NOTES_LIMIT;
  const statusLabel = getMilestoneStatusLabel(milestone.status);

  return (
    <AppLayout>
      <div className="approve-milestone-wrapper">
        <header className="approve-milestone-header">
          <button onClick={() => navigate(-1)} className="approve-milestone-back" aria-label="Go back"><ArrowLeft size={20} /></button>
          <div><span className="approve-milestone-eyebrow">Milestone review</span><h1>Review submitted work</h1><p>{contract.title}</p></div>
        </header>

        {successMessage && <div className="approve-milestone-alert approve-milestone-alert--success" role="status" aria-live="polite">
          <CheckCircle2 size={20} /><p>{successMessage}</p><button onClick={() => setSuccessMessage(null)} aria-label="Dismiss message">&times;</button>
        </div>}
        {error && <div className="approve-milestone-alert approve-milestone-alert--error" role="alert">
          <AlertCircle size={20} /><p>{error}</p><button onClick={() => setError(null)} aria-label="Dismiss error">&times;</button>
        </div>}

        <div className="approve-milestone-layout">
          <main className="approve-milestone-main">
            <section className="approve-milestone-card approve-milestone-overview" aria-labelledby="milestone-title">
              <div className="approve-milestone-section-heading">
                <div><span className="approve-milestone-kicker">Submitted milestone</span><h2 id="milestone-title">{milestone.title}</h2></div>
                <span className={`approve-milestone-status approve-milestone-status--${milestone.status}`}>
                  {isApproved || isPaid ? <CheckCircle2 size={16} /> : <Clock size={16} />}{statusLabel}
                </span>
              </div>
              <div className="approve-milestone-facts">
                <div><span>Due date</span><strong>{formatContractDate(milestone.due_date)}</strong></div>
                <div><span>Current status</span><strong>{statusLabel}</strong></div>
                {milestone.paid_at && <div><span>Payment released</span><strong>{formatContractDate(milestone.paid_at)}</strong></div>}
              </div>
            </section>

            {canApprove && <section className="approve-milestone-card approve-milestone-escrow">
              <button type="button" className="approve-milestone-escrow-toggle" onClick={() => setShowEscrowInfo(!showEscrowInfo)} aria-expanded={showEscrowInfo} aria-controls="escrow-explanation">
                <span className="approve-milestone-icon"><ShieldCheck size={20} /></span>
                <span><strong>Protected by escrow</strong><small>See what happens after approval</small></span>
                <ChevronDown className={showEscrowInfo ? 'is-open' : ''} size={20} />
              </button>
              {showEscrowInfo && <div id="escrow-explanation" className="approve-milestone-escrow-copy">
                Approval authorizes {formatContractAmount(milestone.amount)} to move through the contract's escrow release process. The action is recorded in the contract audit trail.
              </div>}
            </section>}

            <section className="approve-milestone-card" aria-labelledby="deliverables-title">
              <div className="approve-milestone-section-heading">
                <div><span className="approve-milestone-kicker">Review files</span><h2 id="deliverables-title">Submitted deliverables</h2></div>
                <span className="approve-milestone-count">{attachments.length} {attachments.length === 1 ? 'file' : 'files'}</span>
              </div>
              {attachments.length ? <div className="approve-milestone-files">{attachments.map((attachment, index) => {
                const fileName = attachment.file_name?.trim() || `Attachment ${index + 1}`;
                const fileUrl = attachment.file_url?.trim();
                const fileExtension = fileName.includes('.') ? fileName.split('.').pop()?.toUpperCase() : attachment.mime_type?.split('/').pop()?.toUpperCase();
                return <div key={attachment.id || `${attachment.milestone_id}-${index}`} className="approve-milestone-file">
                  <span className="approve-milestone-file-icon"><FileText size={20} /></span>
                  <div className="approve-milestone-file-info"><strong>{fileName}</strong><span>{fileExtension || 'File'} · {fileUrl ? 'Ready to review' : 'Unavailable'}</span></div>
                  <div className="approve-milestone-file-actions">
                    {fileUrl && <a href={fileUrl} target="_blank" rel="noopener noreferrer" aria-label={`Open ${fileName}`} title="Open file"><ExternalLink size={18} /></a>}
                    {fileUrl ? <a href={fileUrl} download={fileName} aria-label={`Download ${fileName}`} title="Download file"><Download size={18} /></a>
                      : <button type="button" aria-label={`${fileName} unavailable`} title="File unavailable" disabled><Download size={18} /></button>}
                  </div>
                </div>;
              })}</div> : <div className="approve-milestone-empty-files"><FileText size={26} /><div><strong>No attached files</strong><p>The freelancer did not attach a downloadable deliverable.</p></div></div>}
            </section>
          </main>

          <aside className="approve-milestone-sidebar">
            <div className="approve-milestone-sidebar-sticky">
              <section className="approve-milestone-card approve-milestone-payment" aria-label="Milestone payment">
                <span><Wallet size={18} /> Milestone value</span><div><GigCoinLogo size={28} /><strong>{formatContractAmount(milestone.amount)}</strong></div>
                <small>{canApprove ? 'Secured in contract escrow' : statusLabel}</small>
              </section>

              {canApprove && <section className="approve-milestone-card approve-milestone-decision" aria-labelledby="decision-title">
                <span className="approve-milestone-kicker">Final step</span><h2 id="decision-title">Make your decision</h2><p>Review the submitted work before choosing an action.</p>
                <div className="approve-milestone-options" role="group" aria-label="Milestone decision">
                  <button type="button" onClick={() => { setApprovalAction('approve'); setApprovalNotes(''); }} className={approvalAction === 'approve' ? 'is-selected is-approve' : ''} aria-pressed={approvalAction === 'approve'}>
                    <CheckCircle2 size={20} /><span><strong>Approve work</strong><small>Accept this delivery</small></span>
                  </button>
                  <button type="button" onClick={() => { setApprovalAction('reject'); setApprovalNotes(''); }} className={approvalAction === 'reject' ? 'is-selected is-revision' : ''} aria-pressed={approvalAction === 'reject'}>
                    <RotateCcw size={20} /><span><strong>Request revision</strong><small>Send it back for changes</small></span>
                  </button>
                </div>
                {approvalAction !== 'pending' && <div className={`approve-milestone-confirmation approve-milestone-confirmation--${approvalAction}`}>
                  {approvalAction === 'approve' ? <p><strong>Confirm approval</strong>This authorizes the escrow release process for {formatContractAmount(milestone.amount)}.</p> : <>
                    <label htmlFor="revision-reason">What needs to be changed? <span>Required</span></label>
                    <textarea id="revision-reason" value={approvalNotes} maxLength={NOTES_LIMIT + 1} onChange={(event) => setApprovalNotes(event.target.value)} placeholder="Describe the specific changes needed..." rows={4} aria-describedby="revision-note revision-count" aria-invalid={notesTooLong} />
                    <div className="approve-milestone-textarea-meta"><small id="revision-note">This reason is used for validation only and is not saved by the current API.</small><span id="revision-count" className={notesTooLong ? 'is-over' : ''}>{approvalNotes.length}/{NOTES_LIMIT}</span></div>
                  </>}
                  <div className="approve-milestone-decision-actions">
                    <button type="button" onClick={() => setApprovalAction('pending')} disabled={isSubmitting}>Cancel</button>
                    <button type="button" className={approvalAction === 'approve' ? 'is-approve' : 'is-revision'} onClick={approvalAction === 'approve' ? handleApprove : handleReject} disabled={isSubmitting || notesTooLong || (approvalAction === 'reject' && !approvalNotes.trim())}>
                      {isSubmitting ? <span className="approve-milestone-spinner approve-milestone-spinner--small" /> : approvalAction === 'approve' ? <CheckCircle2 size={18} /> : <RotateCcw size={18} />}
                      {isSubmitting ? 'Submitting...' : approvalAction === 'approve' ? 'Approve milestone' : 'Request revision'}
                    </button>
                  </div>
                </div>}
              </section>}

              {(isApproved || isPaid) && <section className="approve-milestone-card approve-milestone-complete" role="status">
                <span><CheckCircle2 size={24} /></span><h2>{isPaid ? 'Payment released' : 'Milestone approved'}</h2>
                <p>{isPaid ? `Payment was released on ${formatContractDate(milestone.paid_at || new Date().toISOString())}.` : 'This milestone has been accepted and the escrow release process has started.'}</p>
              </section>}
              <button type="button" onClick={() => navigate(`/workspace/${contractId}`)} className="approve-milestone-workspace-button"><ArrowLeft size={18} /> Back to workspace</button>
            </div>
          </aside>
        </div>
      </div>
    </AppLayout>
  );
}
